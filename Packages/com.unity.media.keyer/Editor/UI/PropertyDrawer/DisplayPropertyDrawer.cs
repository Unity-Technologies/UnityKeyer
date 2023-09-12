using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class DisplayDropdownWrapper
    {
        const string k_Label = "Display";

        IKeyerAccess m_Access;
        UIUtils.DisplayFlags m_CachedDisplayFlags;
        DropdownField m_DropdownField;
        Action<Keyer.Display> m_SetValue;

        readonly List<string> m_Choices = new();

        public DisplayDropdownWrapper(
            DropdownField dropdownField, IKeyerAccess access,
            Keyer.Display initialValue, Action<Keyer.Display> setValue)
        {
            m_DropdownField = dropdownField;

            m_SetValue = setValue;
            m_Access = access;

            m_CachedDisplayFlags = GetDisplayFlags();
            UpdateChoices(m_CachedDisplayFlags, m_Choices);

            m_DropdownField.label = k_Label;
            m_DropdownField.name = k_Label;
            m_DropdownField.choices = m_Choices;

            m_DropdownField.AddToClassList(DropdownField.alignedFieldUssClassName);
            m_DropdownField.RegisterValueChangedCallback(OnValueChanged);

            // make sure initial value is part of the options list.
            if (!m_CachedDisplayFlags.HasFlag(UIUtils.ConvertToFlags(initialValue)))
            {
                var value = UIUtils.GetStringFromDisplay(Keyer.Display.Result);
                m_DropdownField.value = value;
            }
            else
            {
                var value = UIUtils.GetStringFromDisplay(initialValue);
                m_DropdownField.SetValueWithoutNotify(value);
            }

            // We implicitly rely on the automatic invocation of Keyer.OnValidate
            // when a SerializedPropertyChangeEvent is raised by the settings editor.
            // This is implemented in KeyerEditor.
            m_Access.Changed += OnChanged;
        }

        public void Dispose()
        {
            m_Access.Changed -= OnChanged;
            m_DropdownField.UnregisterValueChangedCallback(OnValueChanged);
            m_SetValue = null;
            m_DropdownField = null;
            m_Access = null;
        }

        public Keyer.Display GetValue() => UIUtils.GetDisplayFromString(m_DropdownField.value);

        UIUtils.DisplayFlags GetDisplayFlags()
        {
            var keyer = m_Access.GetKeyer();
            return UIUtils.GetDisplayFlags(keyer == null ? null : keyer.Settings);
        }

        void OnValueChanged(ChangeEvent<string> evt)
        {
            var value = UIUtils.GetDisplayFromString(evt.newValue);
            m_SetValue.Invoke(value);
        }

        // Should be invoked when the settings change,
        // so that we can update the list of available displays if needed.
        void OnChanged()
        {
            var flags = GetDisplayFlags();
            if (flags != m_CachedDisplayFlags)
            {
                var currentValue = m_DropdownField.value;
                UpdateChoices(flags, m_Choices);

                m_DropdownField.choices = m_Choices;

                // We must handle the case where the selected value is not available anymore.
                if (!m_Choices.Contains(currentValue))
                {
                    var value = UIUtils.GetStringFromDisplay(Keyer.Display.Result);
                    m_DropdownField.value = value;
                }

                m_CachedDisplayFlags = flags;
            }
        }

        static void UpdateChoices(UIUtils.DisplayFlags flags, List<string> choices)
        {
            choices.Clear();
            for (var i = 0; i != UIUtils.k_TotalDisplayValues; ++i)
            {
                if (((1 << i) & (int)flags) != 0)
                {
                    var display = (Keyer.Display)i;
                    choices.Add(UIUtils.GetStringFromDisplay(display));
                }
            }
        }
    }

    [CustomPropertyDrawer(typeof(Keyer.Display))]
    class DisplayPropertyDrawer : PropertyDrawer
    {
        DisplayDropdownWrapper m_DropdownWrapper;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var keyerAccess = property.serializedObject.targetObject as IKeyerAccess;
            if (keyerAccess == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(DisplayPropertyDrawer)} initialization failed, " +
                    $"target object should be a non null implementation of {nameof(IKeyerAccess)}");
            }

            var container = new VisualElement();
            var dropdownField = new DropdownField();
            m_DropdownWrapper = new DisplayDropdownWrapper(
                dropdownField, keyerAccess, (Keyer.Display)property.intValue, display =>
                {
                    property.intValue = (int)display;
                    property.serializedObject.ApplyModifiedProperties();
                });
            container.Add(dropdownField);

            return container;
        }
    }
}
