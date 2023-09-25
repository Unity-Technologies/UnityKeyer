using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    /// <summary>
    /// KeyerSettingsEditor is a custom editor for the KeyerSettings component.
    /// </summary>
    [CustomEditor(typeof(KeyerSettings))]
    class KeyerSettingsEditor : UnityEditor.Editor
    {
        static readonly Color k_ColorHeaderBackground = new Color(0.17f, 0.17f, 0.17f, 1);

        KeyerSettings m_KeyerSettings;

        VisualElement m_Root;
        SerializedProperty m_SegmentationAlgorithmCoreProperty;
        SerializedProperty m_SegmentationAlgorithmSoftProperty;
        SerializedProperty m_ColorDifferenceCoreMaskProperty;
        SerializedProperty m_ColorDistanceCoreMaskProperty;
        SerializedProperty m_SoftMaskEnabledProperty;
        SerializedProperty m_ErodeMaskProperty;
        SerializedProperty m_BlurMaskProperty;
        SerializedProperty m_ColorDifferenceSoftMaskProperty;
        SerializedProperty m_ColorDistanceSoftMaskProperty;
        SerializedProperty m_DespillProperty;
        SerializedProperty m_GarbageMaskProperty;
        SerializedProperty m_ClipMaskProperty;
        SerializedProperty m_CropMaskProperty;
        SerializedProperty m_BlendMaskProperty;

        VisualElement m_ColorDifferenceCoreFoldout;
        VisualElement m_ColorDifferenceSoftFoldout;
        VisualElement m_ColorDistanceCoreFoldout;
        VisualElement m_ColorDistanceSoftFoldout;

        void OnEnable()
        {
            if (target == null)
                return;

            m_SegmentationAlgorithmCoreProperty = serializedObject.FindProperty("m_SegmentationAlgorithmCore");
            m_SegmentationAlgorithmSoftProperty = serializedObject.FindProperty("m_SegmentationAlgorithmSoft");
            m_ColorDifferenceCoreMaskProperty = serializedObject.FindProperty("m_ColorDifferenceCoreMask");
            m_ColorDistanceCoreMaskProperty = serializedObject.FindProperty("m_ColorDistanceCoreMask");
            m_ErodeMaskProperty = serializedObject.FindProperty("m_ErodeMask");
            m_BlurMaskProperty = serializedObject.FindProperty("m_BlurMask");
            m_ColorDifferenceSoftMaskProperty = serializedObject.FindProperty("m_ColorDifferenceSoftMask");
            m_ColorDistanceSoftMaskProperty = serializedObject.FindProperty("m_ColorDistanceSoftMask");
            m_SoftMaskEnabledProperty = serializedObject.FindProperty("m_SoftMaskEnabled");
            m_DespillProperty = serializedObject.FindProperty("m_Despill");
            m_GarbageMaskProperty = serializedObject.FindProperty("m_GarbageMask");
            m_ClipMaskProperty = serializedObject.FindProperty("m_ClipMask");
            m_CropMaskProperty = serializedObject.FindProperty("m_CropMask");
            m_BlendMaskProperty = serializedObject.FindProperty("m_BlendMask");
        }

        /// <summary>
        /// Creates the inspector GUI for the KeyerSettings component.
        /// </summary>
        /// <returns>The root VisualElement of the created KeyerSettings GUI.</returns>
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            m_Root = new VisualElement();

            // Core Mask UI
            m_ColorDifferenceCoreFoldout = CreateSegmentationFoldout(
                m_ColorDifferenceCoreMaskProperty, "Core Mask",
                m_SegmentationAlgorithmCoreProperty, OnSegmentationAlgorithmCoreChanged,
                "Use this property to refine larger areas.");
            m_ColorDifferenceCoreFoldout.Add(ErodeCoreMaskToggleFoldout());
            m_Root.Add(m_ColorDifferenceCoreFoldout);

            m_ColorDistanceCoreFoldout = CreateSegmentationFoldout(
                m_ColorDistanceCoreMaskProperty, "Core Mask",
                m_SegmentationAlgorithmCoreProperty, OnSegmentationAlgorithmCoreChanged,
                "Use this property to refine larger areas.");
            m_ColorDistanceCoreFoldout.Add(ErodeCoreMaskToggleFoldout());
            m_Root.Add(m_ColorDistanceCoreFoldout);

            // Soft Mask UI
            m_ColorDifferenceSoftFoldout = CreateSegmentationToggleFoldout(
                m_SoftMaskEnabledProperty, m_ColorDifferenceSoftMaskProperty, "Soft Mask",
                m_SegmentationAlgorithmSoftProperty, OnSegmentationAlgorithmSoftChanged,
                "Use this property to refine smaller or fine details.");
            m_ColorDifferenceSoftFoldout.Add(BlendMaskToggle());
            m_Root.Add(m_ColorDifferenceSoftFoldout);

            m_ColorDistanceSoftFoldout = CreateSegmentationToggleFoldout(
                m_SoftMaskEnabledProperty, m_ColorDistanceSoftMaskProperty, "Soft Mask",
                m_SegmentationAlgorithmSoftProperty, OnSegmentationAlgorithmSoftChanged,
                "Use this property to refine smaller or fine details.");
            m_ColorDistanceSoftFoldout.Add(BlendMaskToggle());
            m_Root.Add(m_ColorDistanceSoftFoldout);

            // Cleanup
            var cleanupToggleFoldout = CleanupToggleFoldout();
            var cleanupTitle = cleanupToggleFoldout.Q<Label>("header-title");
            cleanupTitle.tooltip = "Use this property to get rid of the green spill in the image.";
            m_Root.Add(cleanupToggleFoldout);

            // Cropping UI
            var croppingToggleFoldout = CreateToggleFoldoutWithPropertyField(m_CropMaskProperty, "Cropping");
            var croppingTitle = croppingToggleFoldout.Q<Label>("header-title");
            croppingTitle.tooltip = "The crop option removes elements around the actor not needed in the final composition.";
            m_Root.Add(croppingToggleFoldout);

            // Garbage Mask UI
            var garbageMaskToggleFoldout = CreateToggleFoldoutWithPropertyField(m_GarbageMaskProperty, "Garbage Mask");
            var garbageMaskTitle = garbageMaskToggleFoldout.Q<Label>("header-title");
            garbageMaskTitle.tooltip = "Activate to use a Garbage Mask to mask out parts of your image from view.";
            m_Root.Add(garbageMaskToggleFoldout);

            UpdateSegmentationCoreAlgorithm();
            UpdateSegmentationSoftAlgorithm();

            return m_Root;
        }

        void OnSegmentationAlgorithmCoreChanged(ChangeEvent<string> evt)
        {
            serializedObject.ApplyModifiedProperties();
            UpdateSegmentationCoreAlgorithm();
        }

        void OnSegmentationAlgorithmSoftChanged(ChangeEvent<string> evt)
        {
            serializedObject.ApplyModifiedProperties();
            UpdateSegmentationSoftAlgorithm();
        }

        void UpdateSegmentationCoreAlgorithm()
        {
            var segmentationAlgorithm = (SegmentationAlgorithm)m_SegmentationAlgorithmCoreProperty.intValue;

            var colorDifferenceDisplay = segmentationAlgorithm == SegmentationAlgorithm.ColorDifference ? DisplayStyle.Flex : DisplayStyle.None;
            var colorDistanceDisplay = segmentationAlgorithm == SegmentationAlgorithm.ColorDistance ? DisplayStyle.Flex : DisplayStyle.None;

            m_ColorDifferenceCoreFoldout.style.display = colorDifferenceDisplay;
            m_ColorDistanceCoreFoldout.style.display = colorDistanceDisplay;
        }

        void UpdateSegmentationSoftAlgorithm()
        {
            var segmentationAlgorithm = (SegmentationAlgorithm)m_SegmentationAlgorithmSoftProperty.intValue;

            var colorDifferenceDisplay = segmentationAlgorithm == SegmentationAlgorithm.ColorDifference ? DisplayStyle.Flex : DisplayStyle.None;
            var colorDistanceDisplay = segmentationAlgorithm == SegmentationAlgorithm.ColorDistance ? DisplayStyle.Flex : DisplayStyle.None;

            m_ColorDifferenceSoftFoldout.style.display = colorDifferenceDisplay;
            m_ColorDistanceSoftFoldout.style.display = colorDistanceDisplay;
        }

        ToggleFoldout CleanupToggleFoldout()
        {
            var cleanupToggleFoldout = CreateToggleFoldout(m_DespillProperty, "Cleanup");
            var cleanupField = new PropertyField(m_DespillProperty, "Despill");
            cleanupField.name = "Despill";
            cleanupField.style.flexGrow = 1;
            cleanupField.BindProperty(m_DespillProperty);
            cleanupToggleFoldout.Add(cleanupField);
            return cleanupToggleFoldout;
        }

        static VisualElement CreateSliderWithToggle(string name, string tooltip, SerializedProperty enabledProperty, SerializedProperty sliderProperty, float lowValue, float highValue)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            Toggle toggle = new Toggle();
            toggle.name = name;
            toggle.label = "";
            toggle.tooltip = tooltip;

            toggle.style.marginRight = 3;
            var enabled = enabledProperty;
            toggle.BindProperty(enabled);
            var slider = new Slider();
            slider.name = name;
            slider.label = name;
            slider.tooltip = tooltip;
            slider.lowValue = lowValue;
            slider.highValue = highValue;
            slider.showInputField = true;
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 3;
            slider.AddToClassList(Slider.alignedFieldUssClassName);
            slider.BindProperty(sliderProperty);
            slider.SetEnabled(enabled.boolValue);

            toggle.RegisterValueChangedCallback(evt =>
            {
                slider.SetEnabled(evt.newValue);
            });

            container.Add(toggle);
            container.Add(slider);
            return container;
        }

        VisualElement BlendMaskToggle()
        {
            return CreateSliderWithToggle("Blend Core Mask",
                "Blends the Core Mask and the Soft Mask together. You can use the slider to increase or decrease the strength.",
                m_BlendMaskProperty.FindPropertyRelative("m_Enabled"), m_BlendMaskProperty.FindPropertyRelative("m_Strength"),
                0.0f, 1.0f);
        }

        static void UpdateErodeLabelStyle(GeometryChangedEvent evt)
        {
            var erodeField = evt.target as PropertyField;
            var label = erodeField.Q<Label>();

            // First time we get called, the label is not yet created
            if (label == null)
                return;
            label.style.paddingLeft = 0;
            label.text = "Erode";

            // We only need to be called once
            erodeField.UnregisterCallback<GeometryChangedEvent>(UpdateErodeLabelStyle);
        }

        VisualElement ErodeCoreMaskToggleFoldout()
        {
            const string tooltip = "Erode allows you to gradually increase transparency from the edge of the Keyer inward.";
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            Toggle toggle = new Toggle();
            toggle.name = name;
            toggle.label = "";
            toggle.tooltip = tooltip;
            toggle.style.marginRight = 3;
            toggle.BindProperty(m_ErodeMaskProperty.FindPropertyRelative("m_Enabled"));

            var erodeAmountProperty = m_ErodeMaskProperty.FindPropertyRelative("m_Amount");
            var erodeField = new PropertyField(erodeAmountProperty, "Erode");
            erodeField.BindProperty(erodeAmountProperty);
            erodeField.AddToClassList("unity-inspector-element");
            erodeField.label = "Erode";
            erodeField.tooltip = tooltip;

            toggle.RegisterValueChangedCallback(evt =>
            {
                erodeField.SetEnabled(evt.newValue);
                if (evt.newValue)
                {
                    m_BlurMaskProperty.FindPropertyRelative("m_Enabled").boolValue = true;
                    m_BlurMaskProperty.FindPropertyRelative("m_Quality").intValue = (int)BlurQuality.High;
                    m_ClipMaskProperty.FindPropertyRelative("m_Enabled").boolValue = true;
                }
                else
                {
                    m_BlurMaskProperty.FindPropertyRelative("m_Enabled").boolValue = false;
                    m_ClipMaskProperty.FindPropertyRelative("m_Enabled").boolValue = false;
                }

                serializedObject.ApplyModifiedProperties();
            });

            // Workaround to remove the padding on the label and set the correct text
            erodeField.RegisterCallback<GeometryChangedEvent>(UpdateErodeLabelStyle);

            erodeField.RegisterValueChangeCallback(evt =>
            {
                m_BlurMaskProperty.FindPropertyRelative("m_Enabled").boolValue = true;
                m_BlurMaskProperty.FindPropertyRelative("m_Quality").intValue = (int)BlurQuality.High;
                m_BlurMaskProperty.FindPropertyRelative("m_Radius").floatValue = evt.changedProperty.floatValue;
                m_ClipMaskProperty.FindPropertyRelative("m_ClipBlack").floatValue = evt.changedProperty.floatValue / 30.0f;
                serializedObject.ApplyModifiedProperties();
            });

            container.Add(toggle);
            container.Add(erodeField);
            return container;
        }

        static Foldout CreateSegmentationFoldout(
            SerializedProperty property, string name,
            SerializedProperty segmentationAlgorithmProperty,
            EventCallback<ChangeEvent<string>> onSegmentationAlgorithmChanged,
            string tooltip
        )
        {
            var foldout = new Foldout();
            foldout.text = name;
            var toggle = foldout.Q<Toggle>();
            foldout.viewDataKey = "foldout" + property.name;
            toggle.style.marginLeft = 3;
            toggle.style.backgroundColor = k_ColorHeaderBackground;
            CreateSegmentationPropertyUI(foldout, property, name, segmentationAlgorithmProperty, onSegmentationAlgorithmChanged, tooltip);
            return foldout;
        }

        static ToggleFoldout CreateSegmentationToggleFoldout(
            SerializedProperty enabledProperty, SerializedProperty property, string name,
            SerializedProperty segmentationAlgorithmProperty,
            EventCallback<ChangeEvent<string>> onSegmentationAlgorithmChanged,
            string tooltip
        )
        {
            var toggleFoldout = new ToggleFoldout
            {
                Property = enabledProperty,
                Label = name,
                Enabled = enabledProperty.boolValue
            };
            CreateSegmentationPropertyUI(toggleFoldout, property, name, segmentationAlgorithmProperty, onSegmentationAlgorithmChanged, tooltip);
            return toggleFoldout;
        }

        static void CreateSegmentationPropertyUI(
            VisualElement parent, SerializedProperty property, string name,
            SerializedProperty segmentationAlgorithmProperty,
            EventCallback<ChangeEvent<string>> onSegmentationAlgorithmChanged,
            string tooltip
        )
        {
            var title = parent.Q<Label>();
            parent.AddToClassList("header-back-color");
            title.tooltip = tooltip;
            var segmentationAlgorithmDropDown = new DropdownField("Algorithm");
            segmentationAlgorithmDropDown.AddToClassList(DropdownField.alignedFieldUssClassName);
            segmentationAlgorithmDropDown.tooltip = "Switches the Keying algorithm between Color Difference and Color Distance.";
            segmentationAlgorithmDropDown.BindProperty(segmentationAlgorithmProperty);
            segmentationAlgorithmDropDown.RegisterValueChangedCallback(onSegmentationAlgorithmChanged);
            parent.Add(segmentationAlgorithmDropDown);
            var field = new PropertyField(property, name);
            field.name = name;
            field.style.flexGrow = 1;
            field.BindProperty(property);
            parent.Add(field);
        }

        static ToggleFoldout CreateToggleFoldoutWithPropertyField(SerializedProperty property, string name)
        {
            var toggleFoldout = CreateToggleFoldout(property, name);
            var field = new PropertyField(property, name);
            field.name = name;
            field.style.flexGrow = 1;
            field.BindProperty(property);
            toggleFoldout.Add(field);
            return toggleFoldout;
        }

        static ToggleFoldout CreateToggleFoldout(SerializedProperty property, string name)
        {
            var enabledProperty = property.FindPropertyRelative("m_Enabled");
            var toggleFoldout = new ToggleFoldout
            {
                Property = enabledProperty,
                Label = name,
                Enabled = enabledProperty.boolValue
            };
            return toggleFoldout;
        }
    }
}
