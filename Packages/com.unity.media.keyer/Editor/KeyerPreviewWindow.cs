using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Editor
{
    [Serializable]
    class KeyerPreviewWindow : EditorWindow
    {
        const string k_ErrorNoKeyer = "Select a Keyer instance to preview.";
        const string k_UssPath = "Packages/com.unity.media.keyer/Editor/UI/KeyerPreviewWindow.uss";
        static readonly Color k_ColorSelectedTab = new Color(0.03f, 0.44f, 0.65f);
        static readonly Color k_ColorUnselectedTab = new Color(0.235f, 0.235f, 0.235f);
        readonly KeyerSelection m_KeyerSelection = new();

        DropdownField m_KeyerDropdownField;
        KeyerSelector m_KeyerSelector;
        HelpBox m_NoKeyerHelpBox;

        VisualElement m_TopContainer;
        VisualElement m_TabsContentContainer;
        Toolbar m_Toolbar;

        readonly struct Tab
        {
            public readonly IPreviewTab PreviewTab;
            public readonly ToolbarButton ToolbarButton;
            public readonly VisualElement TabContent;

            public Tab(IPreviewTab previewTab, ToolbarButton toolbarButton, VisualElement tabContent)
            {
                PreviewTab = previewTab;
                ToolbarButton = toolbarButton;
                TabContent = tabContent;
            }
        }

        readonly List<Tab> m_Tabs = new();
        [SerializeField]
        int m_CurrentTab;

        Keyer m_Keyer;
        [SerializeField]
        int m_KeyerInstanceId;

        int m_CachedHashCode;

        [MenuItem("Window/Virtual Production/Keyer Preview Window")]
        static void Init()
        {
            Open(null);
        }

        internal static void Open(Keyer keyer)
        {
            var window = (KeyerPreviewWindow)GetWindow(typeof(KeyerPreviewWindow));
            window.Show();
            window.titleContent = new GUIContent("Keyer Preview Window");
            window.m_KeyerSelector.SetValue(keyer, true);
        }

        void AddTab(Toolbar toolbar, VisualElement tabContentParent, string tabName, IPreviewTab previewTab)
        {
            var index = m_Tabs.Count;
            var toolbarButton = new ToolbarButton();
            toolbarButton.text = tabName;
            toolbarButton.style.paddingTop = 2;
            toolbarButton.userData = index;
            toolbarButton.RegisterCallback<ClickEvent>(OnTabButtonClicked);
            toolbar.Add(toolbarButton);
            var tabContent = previewTab.CreateGUI();
            tabContentParent.Add(tabContent);
            var tabInfo = new Tab(previewTab, toolbarButton, tabContent);
            m_Tabs.Add(tabInfo);
            previewTab.OnEnable();
        }

        void OnTabButtonClicked(ClickEvent evt)
        {
            var evtTarget = evt.target as VisualElement;
            if (evtTarget == null)
                return;
            var tabIndex = evtTarget.userData;

            m_CurrentTab = (int)tabIndex;
            m_TabsContentContainer.Clear();
            m_TabsContentContainer.Add(m_Tabs[m_CurrentTab].TabContent);
            UnselectToolbarButtons();
            m_Tabs[m_CurrentTab].ToolbarButton.style.backgroundColor = k_ColorSelectedTab;
        }

        VisualElement CreateToolbar()
        {
            var toolbarContainer = new VisualElement();
            toolbarContainer.AddToClassList("ui-toolbar-container");
            toolbarContainer.name = "ToolbarContainer";
            m_Toolbar = new Toolbar();
            m_Toolbar.name = "Toolbar";
            m_Toolbar.AddToClassList("ui-toolbar");
            toolbarContainer.Add(m_Toolbar);
            return toolbarContainer;
        }

        VisualElement CreateTabContentContainer()
        {
            m_TabsContentContainer = new VisualElement();
            m_TabsContentContainer.style.flexGrow = 1.0f;
            m_TabsContentContainer.style.flexDirection = FlexDirection.Column;
            m_TabsContentContainer.name = "TabsContentContainer";
            return m_TabsContentContainer;
        }

        VisualElement CreateHeaderContainer()
        {
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("ui-header-container");
            return headerContainer;
        }

        VisualElement CreateKeyerSelector()
        {
            m_KeyerDropdownField = new DropdownField();

            m_KeyerDropdownField.style.marginTop = 5;
            m_KeyerDropdownField.style.marginBottom = 5;

            if (m_KeyerSelector != null)
            {
                m_KeyerSelector.ValueChanged -= OnKeyerComponentChanged;
                m_KeyerSelector.Dispose();
            }

            m_KeyerSelector = new KeyerSelector(m_KeyerDropdownField);
            m_KeyerSelector.ValueChanged += OnKeyerComponentChanged;
            return m_KeyerDropdownField;
        }

        void CreateGUI()
        {
            m_Keyer = EditorUtility.InstanceIDToObject(m_KeyerInstanceId) as Keyer;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath);
            rootVisualElement.styleSheets.Add(styleSheet);
            rootVisualElement.style.flexGrow = 1.0f;
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            m_TopContainer = new VisualElement();
            m_TopContainer.style.flexGrow = 1.0f;
            m_TopContainer.style.flexDirection = FlexDirection.Column;
            m_TopContainer.StretchToParentSize();
            m_TopContainer.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            m_TopContainer.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            var headerContainer = CreateHeaderContainer();
            var toolbarContainer = CreateToolbar();
            var keyerSelector = CreateKeyerSelector();
            headerContainer.Add(keyerSelector);
            headerContainer.Add(toolbarContainer);
            var tabContentContainer = CreateTabContentContainer();
            AddTab(m_Toolbar, tabContentContainer, "Preview", new PreviewTab());
            AddTab(m_Toolbar, tabContentContainer, "Plot Renderer", new PlotRendererTab());

            m_TopContainer.Add(headerContainer);
            SelectTabEntry(m_CurrentTab);
            m_TopContainer.Add(tabContentContainer);
            rootVisualElement.Add(m_TopContainer);
            KeyerComponentChange(m_Keyer);
        }

        void SelectTabEntry(int index)
        {
            m_CurrentTab = index;
            m_TabsContentContainer.Clear();
            m_TabsContentContainer.Add(m_Tabs[index].TabContent);
            UnselectToolbarButtons();
            m_Tabs[index].ToolbarButton.style.backgroundColor = k_ColorSelectedTab;
        }

        void UnselectToolbarButtons()
        {
            foreach (var button in m_Toolbar.Children())
            {
                button.style.backgroundColor = k_ColorUnselectedTab;
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_KeyerSelection.Initialize(keyer =>
            {
                m_KeyerSelector.SetValue(keyer, false);
                OnKeyerComponentChanged(keyer);
            });
            EditorApplication.playModeStateChanged += OnPlaymodeChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            EditorApplication.playModeStateChanged -= OnPlaymodeChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            m_KeyerSelection.Dispose();
        }

        void OnEnable()
        {
            foreach (var tab in m_Tabs)
            {
                tab.PreviewTab.OnEnable();
            }
        }

        void OnDisable()
        {
            foreach (var tab in m_Tabs)
            {
                tab.PreviewTab.OnDisable();
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        void OnPlaymodeChanged(PlayModeStateChange state)
        {
            // Suspend selector registration during playmode to editmode transition.
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                m_KeyerSelector.ValueChanged -= OnKeyerComponentChanged;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (m_KeyerInstanceId != 0)
                {
                    m_Keyer = EditorUtility.InstanceIDToObject(m_KeyerInstanceId) as Keyer;
                }

                m_KeyerSelector.SetValue(m_Keyer, false);
                m_KeyerSelector.ValueChanged += OnKeyerComponentChanged;
                KeyerComponentChange(m_Keyer);
            }
        }

        void OnHierarchyChanged()
        {
            if (m_Keyer == null)
            {
                KeyerComponentChange(null);
                m_KeyerSelector.SetValue(null, false);
            }
        }

        void OnKeyerComponentChanged(Keyer keyer)
        {
            m_Keyer = keyer;
            m_KeyerInstanceId = m_Keyer == null ? 0 : m_Keyer.GetInstanceID();
            KeyerComponentChange(m_Keyer);
        }

        void KeyerComponentChange(Keyer newKeyer)
        {
            m_CachedHashCode = GetKeyerHashCode(newKeyer);

            foreach (var tab in m_Tabs)
            {
                tab.PreviewTab.SetKeyer(newKeyer);
            }

            if (m_NoKeyerHelpBox != null)
            {
                m_NoKeyerHelpBox.style.display = DisplayStyle.None;
            }

            if (newKeyer == null)
            {
                if (m_NoKeyerHelpBox == null)
                {
                    m_NoKeyerHelpBox = new HelpBox(k_ErrorNoKeyer, HelpBoxMessageType.Error);
                    m_TabsContentContainer.Add(m_NoKeyerHelpBox);
                }
                else
                {
                    m_NoKeyerHelpBox.style.display = DisplayStyle.Flex;
                }
            }
        }

        void Update()
        {
            if (m_Keyer != null && GetKeyerHashCode(m_Keyer) != m_CachedHashCode)
            {
                KeyerComponentChange(m_Keyer);
            }
        }

        static int GetKeyerHashCode(Keyer keyer)
        {
            static int GetNullableHashCode<T>(T obj) where T : Object => obj == null ? 0 : obj.GetHashCode();

            if (keyer == null)
            {
                return 0;
            }

            unchecked
            {
                var hashCode = 1768953197;
                hashCode = (hashCode * 397) ^ GetNullableHashCode(keyer.Foreground);
                hashCode = (hashCode * 397) ^ GetNullableHashCode(keyer.Result);
                hashCode = (hashCode * 397) ^ GetNullableHashCode(keyer.Settings);
                return hashCode;
            }
        }
    }
}
