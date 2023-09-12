using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

//TODO: fix uss classes composition labelling
//TODO: inherit from BindableElement and INotifyValueChanged<bool> like in Foldout class ?
class ToggleFoldout : VisualElement
{
    static readonly string ussClassName = "unity-foldout-depth-0";
    static readonly string ussClassName2 = "unity-foldout-toggle-inspector";

    Label m_Title;
    Toggle m_FoldoutToggle;
    Toggle m_SwitchToggle;
    VisualElement m_FoldoutBag;
    SerializedProperty m_SerializedProperty;

    public override VisualElement contentContainer => m_FoldoutBag;
    public EventCallback<bool> ToggleValueChanged;
    public Label Title => m_Title;
    public Toggle FoldoutToggle => m_FoldoutToggle;
    public Toggle SwitchToggle => m_SwitchToggle;

    [NotNull]
    public SerializedProperty Property
    {
        get => m_SerializedProperty;
        set
        {
            m_SerializedProperty = value ?? throw new ArgumentNullException(nameof(value));
            viewDataKey = "toggle-foldout-" + Property.name;
            FoldoutToggle.viewDataKey = "foldout-" + Property.name;
            SwitchToggle.BindProperty(m_SerializedProperty);
        }
    }

    public string Label
    {
        get => Title.text;
        set => Title.text = value;
    }

    public bool Enabled
    {
        get => m_SwitchToggle.value;
        set => SetSwitchValueWithoutNotify(value);
    }

    public ToggleFoldout()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.media.keyer/Editor/UI/ToggleFoldout/ToggleFoldout.uxml");
        var visualElementAsset = tree.Instantiate();

        m_Title = visualElementAsset.Q<Label>("header-title");
        visualElementAsset.AddToClassList(ussClassName);

        m_FoldoutToggle = visualElementAsset.Q<Toggle>("dropdown-toggle");
        m_FoldoutToggle.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue == false)
            {
                m_FoldoutBag.style.display = DisplayStyle.None;
                m_FoldoutBag.visible = false;
            }
            else
            {
                m_FoldoutBag.style.display = DisplayStyle.Flex;
                m_FoldoutBag.visible = true;
            }
        });

        m_SwitchToggle = visualElementAsset.Q<Toggle>("state-toggle");
        m_SwitchToggle.value = Enabled;
        m_SwitchToggle.RegisterValueChangedCallback(evt =>
        {
            m_FoldoutBag.SetEnabled(evt.newValue != false);
            Property.boolValue = evt.newValue;
            if (ToggleValueChanged != null)
            {
                ToggleValueChanged(evt.newValue);
            }
        });

        m_FoldoutBag = visualElementAsset.Q("foldout-bag-container");
        m_FoldoutBag.AddToClassList(Foldout.contentUssClassName);
        visualElementAsset.Q("header-container").AddToClassList(ussClassName2);

        m_FoldoutBag.SetEnabled(m_SwitchToggle.value != false);
        hierarchy.Add(visualElementAsset);

        Title.text = Label;
    }

    public void SetFoldoutValueWithoutNotify(bool newValue)
    {
        m_FoldoutToggle.value = newValue;
        contentContainer.style.display = newValue ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetSwitchValueWithoutNotify(bool newValue)
    {
        m_SwitchToggle.value = newValue;
        m_FoldoutBag.SetEnabled(newValue);
    }

    public new class UxmlFactory : UxmlFactory<ToggleFoldout, UxmlTraits> { }

    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        UxmlBoolAttributeDescription m_FoldoutToggle =
            new UxmlBoolAttributeDescription { name = "foldout-value" };

        UxmlBoolAttributeDescription m_SwitchToggle =
            new UxmlBoolAttributeDescription { name = "switch-value" };

        public override IEnumerable<UxmlChildElementDescription> uxmlChildElementsDescription
        {
            get { yield break; }
        }

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            if (ve is ToggleFoldout foldout)
            {
                foldout.m_FoldoutToggle.value = m_FoldoutToggle.GetValueFromBag(bag, cc);
            }
        }
    }
}
