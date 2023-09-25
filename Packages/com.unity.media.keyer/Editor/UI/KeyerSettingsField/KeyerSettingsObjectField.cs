using System;
using Unity.Media.Keyer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// TODO See if PropertyFieldDrawer may not be useful here.
class KeyerSettingsObjectField : VisualElement
{
    ObjectField m_KeyerSettingsField;
    public ObjectField KeyerSettingsField => m_KeyerSettingsField;

    public KeyerSettings KeyerSettings
    {
        get => (KeyerSettings)m_KeyerSettingsField.value;
        set => m_KeyerSettingsField.value = value;
    }

    Button m_SettingsCreatorButton;

    public KeyerSettingsObjectField()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.media.keyer/Editor/UI/KeyerSettingsField/NewObjectField.uxml");
        var visualTreeElementAsset = tree.Instantiate();

        m_KeyerSettingsField = visualTreeElementAsset.Q<ObjectField>("object-field");
        m_KeyerSettingsField.objectType = typeof(KeyerSettings);
        m_KeyerSettingsField.tooltip = "Optionally drag and drop a Keyer profile from a saved asset to load its default settings.";
        m_KeyerSettingsField.AddToClassList(ObjectField.alignedFieldUssClassName);

        m_SettingsCreatorButton = visualTreeElementAsset.Q<Button>("new-object-button");
        m_SettingsCreatorButton.clicked += CreateNewSettings;

        hierarchy.Add(visualTreeElementAsset);
    }

    void CreateNewSettings()
    {
        var keyerSettings = ScriptableObject.CreateInstance<KeyerSettings>();
        m_KeyerSettingsField.value = keyerSettings;

        var uniquePath = AssetDatabase.GenerateUniqueAssetPath("Assets/KeyerSettings.asset");
        AssetDatabase.CreateAsset(m_KeyerSettingsField.value, uniquePath);
    }

    public new class UxmlFactory : UxmlFactory<KeyerSettingsObjectField, UxmlTraits> { }
}
