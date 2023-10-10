using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class KeyerSelection
    {
        Action<Keyer> m_AssignKeyer;

        public void Initialize(Action<Keyer> assignKeyer)
        {
            m_AssignKeyer = assignKeyer;
            OnSelectionChanged();
            Selection.selectionChanged += OnSelectionChanged;
        }

        public void Dispose()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            m_AssignKeyer = null;
        }

        void OnSelectionChanged()
        {
            if (TryGetSelectedKeyer(out var keyer))
            {
                m_AssignKeyer.Invoke(keyer);
            }
        }

        public static bool TryGetSelectedKeyer(out Keyer keyer)
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go.activeInHierarchy && go.TryGetComponent(out keyer))
                {
                    if (keyer.enabled)
                    {
                        return true;
                    }
                }
            }

            keyer = null;
            return false;
        }
    }

    class KeyerSelector
    {
        const string k_NoneLabel = "None"; // TODO More user friendly.

        static readonly Dictionary<string, Keyer> k_NameToInstance = new();
        static readonly Dictionary<Keyer, string> k_InstanceToName = new();
        static readonly Dictionary<Keyer, string> k_InstanceToOriginalName = new();
        static readonly Dictionary<string, int> k_NameTrack = new();
        static readonly List<string> k_Choices = new();
        static string[] s_ChoicesArray;
        static event Action AvailableInstancesChanged = delegate { };

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            OnActiveInstancesChanged();
            Keyer.ActiveInstancesChanged += OnActiveInstancesChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        // Suspending registration before assembly reload avoids notifiably
        // clearing the list of available instances only to repopulate it similarly right after.
        static void OnBeforeAssemblyReload()
        {
            Keyer.ActiveInstancesChanged -= OnActiveInstancesChanged;
        }

        static void OnHierarchyChanged()
        {
            // Update the caches if an instance was renamed.
            foreach (var pair in k_InstanceToOriginalName)
            {
                if (pair.Key.gameObject.name != pair.Value)
                {
                    OnActiveInstancesChanged();
                    return;
                }
            }
        }

        static void OnActiveInstancesChanged()
        {
            k_NameToInstance.Clear();
            k_InstanceToName.Clear();
            k_InstanceToOriginalName.Clear();
            k_NameTrack.Clear();
            k_Choices.Clear();

            // We must support no instance selected.
            k_Choices.Add(k_NoneLabel);

            // Here we build a map of active instances.
            // The user will select based on name, but we must be ready for multiple instances having the same name.
            foreach (var instance in Keyer.ActiveInstances)
            {
                var name = instance.gameObject.name;
                k_InstanceToOriginalName.Add(instance, name);

                if (k_NameTrack.TryGetValue(name, out var nameUseCount))
                {
                    ++nameUseCount;
                    k_NameTrack[name] = nameUseCount;
                }
                else
                {
                    k_NameTrack.Add(name, 1);
                }

                // 2nd occcurence get renamed "thename (2)" and so on.
                if (nameUseCount > 0)
                {
                    name = $"{name} ({nameUseCount})";
                }

                k_Choices.Add(name);
                k_NameToInstance.Add(name, instance);
                k_InstanceToName.Add(instance, name);
            }

            s_ChoicesArray = k_Choices.ToArray();

            AvailableInstancesChanged.Invoke();
        }

        // Selected keyer must among the list of active ones.
        static bool TrySelect(ref Keyer keyer, out string name)
        {
            if (keyer != null && k_InstanceToName.TryGetValue(keyer, out name))
            {
                return true;
            }

            name = k_NoneLabel;
            keyer = null;
            return false;
        }

        const string k_Label = "Keyer Instance";
        static readonly GUIContent k_LabelGuiContent = new(k_Label);

        // Integration with ImGUI.
        public static Keyer Popup(Keyer instance)
        {
            var keyer = instance;
            TrySelect(ref keyer, out var name);
            var index = Array.IndexOf(s_ChoicesArray, name);
            var newIndex = EditorGUILayout.Popup(k_LabelGuiContent, index, s_ChoicesArray);

            if (k_NameToInstance.TryGetValue(s_ChoicesArray[newIndex], out var result))
            {
                return result;
            }

            return null;
        }

        bool m_Disposed;
        Keyer m_Keyer;
        DropdownField m_DropdownField;

        public event Action<Keyer> ValueChanged = delegate { };

        // Do not allow setting an initial value in the constructor.
        // Client code must've had a chance to register ValueChanged callback first,
        // due to restrictions on available Keyer instances.
        public KeyerSelector(DropdownField dropdownField)
        {
            m_DropdownField = dropdownField;

            m_DropdownField.label = k_Label;
            m_DropdownField.name = k_Label;
            m_DropdownField.choices = k_Choices;
            m_DropdownField.value = k_NoneLabel;

            AvailableInstancesChanged += OnAvailableInstancesChanged;
            m_DropdownField.RegisterValueChangedCallback(OnValueChanged);
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_DropdownField.UnregisterValueChangedCallback(OnValueChanged);
            AvailableInstancesChanged -= OnAvailableInstancesChanged;
            m_Keyer = null;
            m_DropdownField = null;

            m_Disposed = true;
        }

        ~KeyerSelector()
        {
            if (!m_Disposed)
            {
                Dispose();
            }
        }

        public void SetValue(Keyer keyer, bool notify)
        {
            m_Keyer = keyer;
            var canSelectInstance = TrySelect(ref m_Keyer, out var name);
            m_DropdownField.SetValueWithoutNotify(name);

            if (!canSelectInstance)
            {
                // At this point m_Keyer = null.
                ValueChanged.Invoke(null);
            }
            else if (notify)
            {
                ValueChanged.Invoke(m_Keyer);
            }
        }

        public Keyer GetValue() => m_Keyer;

        void OnAvailableInstancesChanged()
        {
            // Is it possible that the instance we has selected is not available anymore.
            // Ot that its name changed.
            SetValue(m_Keyer, false);
        }

        void OnValueChanged(ChangeEvent<string> evt)
        {
            if (k_NameToInstance.TryGetValue(evt.newValue, out var keyer))
            {
                m_Keyer = keyer;
                ValueChanged.Invoke(m_Keyer);
                return;
            }

            if (evt.newValue == k_NoneLabel)
            {
                m_Keyer = null;
                ValueChanged.Invoke(null);
                return;
            }

            // We'd have a de-synchronization between static registries and selected instance which should not happen.
            throw new InvalidOperationException(
                $"Could not find {nameof(Keyer)} instance matching name \"{evt.newValue}\"");
        }
    }
}
