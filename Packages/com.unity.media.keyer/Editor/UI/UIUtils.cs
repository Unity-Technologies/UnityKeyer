using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class UIUtils
    {
        [Flags]
        public enum DisplayFlags
        {
            None = 0,
            Result = 1 << 0,
            CoreMatte = 1 << 1,
            SoftMatte = 1 << 2,
            ErodeMatte = 1 << 3,
            BlendMax = 1 << 4,
            Front = 1 << 5,
            GarbageMask = 1 << 6,
            Despill = 1 << 7,
            CropMatte = 1 << 8,
            All = ~0
        }

        static readonly Keyer.Display[] k_AllDisplays =
        {
            Keyer.Display.Result,
            Keyer.Display.CoreMatte,
            Keyer.Display.SoftMatte,
            Keyer.Display.ErodeMatte,
            Keyer.Display.BlendMax,
            Keyer.Display.Front,
            Keyer.Display.GarbageMask,
            Keyer.Display.Despill,
            Keyer.Display.CropMatte
        };

        public static DisplayFlags ConvertToFlags(Keyer.Display display)
        {
            var index = (int)display;
            return (DisplayFlags)(1 << index);
        }

        // Must be kept in sync with Keyer.Display.
        public const int k_TotalDisplayValues = 9;

        static readonly Dictionary<string, Keyer.Display> k_StringToDisplay = new();
        static readonly Dictionary<Keyer.Display, string> k_DisplayToString = new();

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            // If we used user-friendly names based on attributes, we would handle it here.
            foreach (var value in k_AllDisplays)
            {
                k_StringToDisplay.Add(value.ToString(), value);
                k_DisplayToString.Add(value, value.ToString());
            }
        }

        public static string GetStringFromDisplay(Keyer.Display display)
        {
            if (k_DisplayToString.TryGetValue(display, out var name))
            {
                return name;
            }

            throw new InvalidOperationException(
                $"Could not find name matching {nameof(Keyer.Display)} \"{display}\".");
        }

        public static Keyer.Display GetDisplayFromString(string name)
        {
            if (k_StringToDisplay.TryGetValue(name, out var display))
            {
                return display;
            }

            throw new InvalidOperationException(
                $"Could not find {nameof(Keyer.Display)} value matching \"{name}\".");
        }

        public static DisplayFlags GetDisplayFlags(KeyerSettings settings)
        {
            static DisplayFlags FromBool(bool value) => value ? DisplayFlags.All : DisplayFlags.None;

            // Result, Core and Front are available at all times providing an input texture has been provided.
            var result = DisplayFlags.Result | DisplayFlags.CoreMatte | DisplayFlags.Front;

            // We choose maximum resilience, when the settings are null,
            // we still have a list of always available options.
            if (settings == null)
            {
                return result;
            }

            result |= DisplayFlags.SoftMatte & FromBool(settings.SoftMaskEnabled);
            result |= DisplayFlags.ErodeMatte & FromBool(settings.ErodeMask.Enabled);
            result |= DisplayFlags.BlendMax & FromBool(settings.BlendMask.Enabled);
            result |= DisplayFlags.GarbageMask & FromBool(settings.GarbageMask.Enabled);
            result |= DisplayFlags.Despill & FromBool(settings.Despill.Enabled);
            result |= DisplayFlags.CropMatte & FromBool(settings.CropMask.Enabled);
            return result;
        }

        public static VisualElement CreateKeyerInspector(Keyer keyer, VisualElement parent)
        {
            var inspectorScrollView = new ScrollView(ScrollViewMode.Vertical);
            inspectorScrollView.style.flexGrow = 1;
            inspectorScrollView.style.flexShrink = 1;

            parent.Add(inspectorScrollView);
            // The unity-inspector-element is required to make the inspector alignment work,
            // using the alignedFieldUssClassName is not enough.
            parent.AddToClassList("unity-inspector-element");
            var keyerEditor = UnityEditor.Editor.CreateEditor(keyer).CreateInspectorGUI();
            keyerEditor.style.minWidth = 300;
            keyerEditor.style.minHeight = 600;
            var displayContainer = keyerEditor.Q("display-container");
            displayContainer?.RemoveFromHierarchy();
            inspectorScrollView.Add(keyerEditor);
            return inspectorScrollView;
        }
    }
}
