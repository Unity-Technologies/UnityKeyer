using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [CustomPropertyDrawer(typeof(Crop))]
    class CropMaskPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Create property container element.
            var container = new VisualElement();

            // Create property fields.
            var topFloatField = new PropertyField(property.FindPropertyRelative("m_Top"));
            var bottomFloatField = new PropertyField(property.FindPropertyRelative("m_Bottom"));
            var leftFloatField = new PropertyField(property.FindPropertyRelative("m_Left"));
            var rightFloatField = new PropertyField(property.FindPropertyRelative("m_Right"));

            // Add fields to the container.
            container.Add(topFloatField);
            container.Add(bottomFloatField);
            container.Add(leftFloatField);
            container.Add(rightFloatField);
            return container;
        }
    }
}
