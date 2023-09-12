using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class DisplayMaskFieldWrapper
    {
        IKeyerAccess m_Access;
        UIUtils.DisplayFlags m_SelectedDisplays;
        UIUtils.DisplayFlags m_CachedDisplayFlags;
        MaskField m_MaskField;
        Action<UIUtils.DisplayFlags> m_SetValue;

        readonly List<string> m_Choices = new();
        readonly List<int> m_ChoicesMasks = new();

        public DisplayMaskFieldWrapper(
            MaskField maskField, string label, IKeyerAccess access,
            UIUtils.DisplayFlags initialValue, Action<UIUtils.DisplayFlags> setValue)
        {
            m_MaskField = maskField;
            m_SetValue = setValue;
            m_Access = access;

            m_CachedDisplayFlags = GetDisplayFlags();
            UpdateChoicesAndChoicesMask(m_CachedDisplayFlags, m_Choices, m_ChoicesMasks);

            m_MaskField.label = label;
            m_MaskField.name = label;
            m_MaskField.choices = m_Choices;
            m_MaskField.choicesMasks = m_ChoicesMasks;
            m_MaskField.RegisterCallback<ChangeEvent<int>>(OnSelectionChanged);

            // make sure initial value is part of the options list.
            if (!m_CachedDisplayFlags.HasFlag(initialValue))
            {
                m_SelectedDisplays = UIUtils.DisplayFlags.CoreMatte;
                m_MaskField.value = (int)m_SelectedDisplays;
            }
            else
            {
                m_SelectedDisplays = initialValue;
                m_MaskField.SetValueWithoutNotify((int)m_SelectedDisplays);
            }

            // We implicitly rely on the automatic invocation of Keyer.OnValidate
            // when a SerializedPropertyChangeEvent is raised by the settings editor.
            // This is implemented in KeyerEditor.
            m_Access.Changed += OnKeyerSettingsChanged;
        }

        static void UpdateChoicesAndChoicesMask(UIUtils.DisplayFlags flags, List<string> choices, List<int> choiceMask)
        {
            choices.Clear();
            choiceMask.Clear();
            for (var i = 0; i != UIUtils.k_TotalDisplayValues; ++i)
            {
                if (((1 << i) & (int)flags) != 0)
                {
                    var display = (Keyer.Display)i;
                    choices.Add(UIUtils.GetStringFromDisplay(display));
                    choiceMask.Add(1 << i);
                }
            }
        }

        static UIUtils.DisplayFlags UpdateSelectedDisplaysFromAvailableDisplays(UIUtils.DisplayFlags selectedDisplays, UIUtils.DisplayFlags availableDisplays)
        {
            for (var i = 0; i != UIUtils.k_TotalDisplayValues; ++i)
            {
                if (((1 << i) & (int)selectedDisplays) != 0)
                {
                    // if the selected display is not available, remove it from the selection
                    if (((1 << i) & (int)availableDisplays) == 0)
                    {
                        selectedDisplays &= ~(UIUtils.DisplayFlags)(1 << i);
                    }
                }
            }
            return selectedDisplays;
        }

        void UpdateDisplayChoices(UIUtils.DisplayFlags flags)
        {
            UpdateChoicesAndChoicesMask(flags, m_Choices, m_ChoicesMasks);
            m_MaskField.choices = m_Choices;
            m_MaskField.choicesMasks = m_ChoicesMasks;
            m_SelectedDisplays = UpdateSelectedDisplaysFromAvailableDisplays(m_SelectedDisplays, flags);
            m_MaskField.SetValueWithoutNotify((int)m_SelectedDisplays);
        }

        public void Dispose()
        {
            m_Access.Changed -= OnKeyerSettingsChanged;
            m_MaskField.UnregisterCallback<ChangeEvent<int>>(OnSelectionChanged);
            m_SetValue = null;
            m_MaskField = null;
            m_Access = null;
        }

        public UIUtils.DisplayFlags GetSelectedDisplays() => m_SelectedDisplays;

        UIUtils.DisplayFlags GetDisplayFlags()
        {
            var keyer = m_Access.GetKeyer();
            return UIUtils.GetDisplayFlags(keyer == null ? null : keyer.Settings);
        }

        void OnSelectionChanged(ChangeEvent<int> evt)
        {
            m_SelectedDisplays = (UIUtils.DisplayFlags)evt.newValue;
            m_SetValue.Invoke(m_SelectedDisplays);
        }

        // Should be invoked when the settings change,
        // so that we can update the list of available displays if needed.
        void OnKeyerSettingsChanged()
        {
            var flags = GetDisplayFlags();
            if (flags != m_CachedDisplayFlags)
            {
                UpdateDisplayChoices(flags);
                m_CachedDisplayFlags = flags;
                m_SetValue.Invoke(m_SelectedDisplays);
            }
        }
    }
}
