using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [CustomPropertyDrawer(typeof(ColorDifference))]
    class ColorDifferencePropertyDrawer : PropertyDrawer
    {
        const float k_RangeMin = 0;
        const float k_RangeMax = 20;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create property container element.
            var container = new VisualElement();

            // Create property fields.
            var backgroundColorField = new PropertyField(property.FindPropertyRelative("m_BackgroundColor"), "Background Color");
            backgroundColorField.tooltip = "The solid background color.";

            var scale = property.FindPropertyRelative("m_Scale");

            Slider ConfigureScaleSlider(string propName, string label, string tooltip)
            {
                var slider = new Slider();
                slider.BindProperty(scale.FindPropertyRelative(propName));
                slider.label = label;
                slider.lowValue = k_RangeMin;
                slider.highValue = k_RangeMax;
                slider.showInputField = true;
                slider.tooltip = tooltip;
                slider.AddToClassList(Slider.alignedFieldUssClassName);
                return slider;
            }

            var redSlider = ConfigureScaleSlider("x", "Scale Red", "Scale the red channel of the input image to modulate the color difference equation.");
            var greenSlider = ConfigureScaleSlider("y", "Scale Green", "Scale the green channel of the input image to modulate the color difference equation.");
            var blueSlider = ConfigureScaleSlider("z", "Scale Blue", "Scale the blue channel of the input image to modulate the color difference equation.");

            var clipWhiteField = new PropertyField(property.FindPropertyRelative("m_ClipWhite"), "Clip White");
            var clipBlackField = new PropertyField(property.FindPropertyRelative("m_ClipBlack"), "Clip Black");

            // Add fields to the container.
            container.Add(backgroundColorField);
            container.Add(redSlider);
            container.Add(greenSlider);
            container.Add(blueSlider);
            container.Add(clipWhiteField);
            container.Add(clipBlackField);
            return container;
        }
    }
}
