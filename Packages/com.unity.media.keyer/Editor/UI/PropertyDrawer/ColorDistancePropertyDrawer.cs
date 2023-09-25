using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [CustomPropertyDrawer(typeof(ColorDistance))]
    class ColorDistancePropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            var keyColorField = new PropertyField(property.FindPropertyRelative("m_ChromaKey"));
            keyColorField.tooltip = "Select the chroma color of the background.";

            var thresholdSlider = new MinMaxSlider("Threshold Range", 0.1f, .9f, 0, 1);
            thresholdSlider.tooltip = "The range separating foreground and background.";
            thresholdSlider.style.flexGrow = 1;
            thresholdSlider.AddToClassList(MinMaxSlider.alignedFieldUssClassName);
            thresholdSlider.BindProperty(property.FindPropertyRelative("m_Threshold"));

            container.Add(keyColorField);
            container.Add(thresholdSlider);
            return container;
        }
    }
}
