using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [CustomPropertyDrawer(typeof(Despill))]
    class DespillPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create property container element.
            var container = new VisualElement();

            // Create property fields.
            var backgroundColorField = new PropertyField(property.FindPropertyRelative("m_BackgroundColor"), "Background Color");
            backgroundColorField.tooltip = "The solid background color.";
            var despillField = new PropertyField(property.FindPropertyRelative("m_DespillAmount"), "Despill");

            // Add fields to the container.
            container.Add(backgroundColorField);
            container.Add(despillField);
            return container;
        }
    }
}
