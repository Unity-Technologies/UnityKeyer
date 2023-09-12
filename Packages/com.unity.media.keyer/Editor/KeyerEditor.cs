using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    /// <summary>
    /// KeyerEditor is a custom editor for the Keyer component.
    /// </summary>
    [CustomEditor(typeof(Keyer))]
    class KeyerEditor : UnityEditor.Editor
    {
        UnityEditor.Editor m_SettingEditor;

        SerializedProperty m_ForegroundProperty;
        SerializedProperty m_ResultProperty;
        SerializedProperty m_DisplayProperty;
        SerializedProperty m_SettingsProperty;

        Keyer m_Keyer;

        VisualElement m_Root;
        VisualElement m_SettingsEditor;

        void OnEnable()
        {
            if (target == null)
                return;

            m_ForegroundProperty = serializedObject.FindProperty("m_Foreground");
            m_ResultProperty = serializedObject.FindProperty("m_Result");
            m_DisplayProperty = serializedObject.FindProperty("m_DisplayMode");
            m_SettingsProperty = serializedObject.FindProperty("m_Settings");
        }

        /// <summary>
        /// Creates the inspector GUI for the Keyer component.
        /// </summary>
        /// <returns>The root VisualElement of the created inspector GUI.</returns>
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();

            m_Keyer = (Keyer)target;

            m_Root = new VisualElement();

#if DEBUG_ADVANCED
            var captureButton = new Button { text = "RenderDoc Capture" };
            captureButton.RegisterCallback<ClickEvent>(_ =>
            {
                m_Keyer.RequestRenderDocCapture();
                EditorApplication.QueuePlayerLoopUpdate();
            });
            m_Root.Add(captureButton);

            var sdfCaptureButton = new Button { text = "Sdf RenderDoc Capture" };
            sdfCaptureButton.RegisterCallback<ClickEvent>(_ =>
            {
                m_Keyer.RequestSdfRenderDocCapture();
                EditorApplication.QueuePlayerLoopUpdate();
            });
            m_Root.Add(sdfCaptureButton);
#endif

            var foregroundField = new PropertyField(m_ForegroundProperty, "Foreground");
            foregroundField.Bind(serializedObject);
            foregroundField.tooltip = "The input Foreground green screen texture.";
            m_Root.Add(foregroundField);

            var resultTextureField = new PropertyField(m_ResultProperty, "Result Texture");
            resultTextureField.Bind(serializedObject);
            resultTextureField.tooltip = "The Keyer resulting render texture.";
            m_Root.Add(resultTextureField);

            var container = new VisualElement();
            container.name = "display-container";
            container.style.flexDirection = FlexDirection.Row;
            m_Root.Add(container);

            var displayField = new PropertyField(m_DisplayProperty);
            displayField.style.flexShrink = 1;
            displayField.style.flexBasis = 100;
            displayField.style.flexGrow = 1;
            displayField.tooltip = "Use to visualize the intermediate results of the pipeline.";
            container.Add(displayField);

            var openPreviewButton = new Button(OpenPreview)
            {
                text = "Preview"
            };
            openPreviewButton.style.minWidth = 80;
            openPreviewButton.tooltip = "Opens the Keyer preview window.";
            container.Add(openPreviewButton);

            var settingField = new KeyerSettingsObjectField();
            settingField.KeyerSettingsField.BindProperty(m_SettingsProperty);
            settingField.tooltip = "Create a new Keyer settings.";

            m_Root.Add(settingField);

            settingField.KeyerSettingsField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(m_Keyer, "Assign Keyer Settings.");
                m_Keyer.Settings = settingField.KeyerSettings;
                serializedObject.ApplyModifiedProperties();
                m_SettingsEditor?.RemoveFromHierarchy();
                m_SettingsEditor = null;
                CreateSettingEditor();
            });

            settingField.KeyerSettings = m_Keyer.Settings;
            CreateSettingEditor();

            return m_Root;
        }

        void OpenPreview()
        {
            KeyerPreviewWindow.Open(m_Keyer);
        }

        void CreateSettingEditor()
        {
            if (m_Keyer.Settings != null)
            {
                m_SettingsEditor = CreateEditor(m_Keyer.Settings).CreateInspectorGUI();

                m_SettingsEditor.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                {
                    m_Keyer.OnValidate();
                });
                m_SettingsEditor.RegisterCallback<ChangeEvent<bool>>(evt =>
                {
                    m_Keyer.OnValidate();
                });
                m_SettingsEditor.RegisterCallback<ChangeEvent<string>>(evt =>
                {
                    m_Keyer.OnValidate();
                });

                m_Root.Add(m_SettingsEditor);
            }
        }
    }
}
