using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [Serializable]
    class PreviewTab : IPreviewTab
    {
        const int k_MinWidth = 300;
        const string k_PanZoomInfo = "Use the mouse wheel to zoom and drag to pan. Use ALT/Option to pan and zoom only one viewport.";

        readonly List<KeyerPreviewViewport> m_KeyerPreviewVp = new();
        VisualElement m_KeyerEditor;
        VisualElement m_TopContainer;
        VisualElement m_KeyerEditorContainer;
        TwoPaneSplitView m_KeyerVpContainer;
        VisualElement m_InspectorScrollView;
        Keyer m_Keyer;
        HelpBox m_ModifierInfoBox;

        static bool DisplayModifierInfo
        {
            get => EditorPrefs.GetBool("Keyer.DisplayModifierInfo", true);
            set => EditorPrefs.SetBool("Keyer.DisplayModifierInfo", value);
        }

        public VisualElement GetVisualElement()
        {
            return m_TopContainer;
        }

        internal void PreviewTransformChanged(TexturePreview.Transform trs)
        {
            foreach (var kp in m_KeyerPreviewVp)
            {
                kp.TexturePreview.SetNormalizedTransform(trs);
            }
        }

        void OnModifierPressed()
        {
            DisplayModifierInfo = false;
            foreach (var kp in m_KeyerPreviewVp)
            {
                kp.TexturePreview.ModifierPressed -= OnModifierPressed;
            }
            m_ModifierInfoBox?.RemoveFromHierarchy();
        }

        VisualElement CreateKeyerViewportContainer()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.flexGrow = 1;
            container.style.minWidth = k_MinWidth;
            container.style.width = k_MinWidth;
            return container;
        }

        void CreateKeyerLeftViewports(VisualElement parent)
        {
            var rightViewports = new TwoPaneSplitView(0, k_MinWidth, TwoPaneSplitViewOrientation.Vertical);
            rightViewports.style.flexGrow = 1;
            rightViewports.style.minWidth = k_MinWidth;
            rightViewports.style.width = k_MinWidth;
            parent.Add(rightViewports);

            var topRightViewport = CreateKeyerViewportContainer();
            var bottomRightViewport = CreateKeyerViewportContainer();

            rightViewports.Add(topRightViewport);
            rightViewports.Add(bottomRightViewport);
            m_KeyerPreviewVp.Add(new KeyerPreviewViewport(topRightViewport, m_Keyer, Keyer.Display.CoreMatte));
            m_KeyerPreviewVp.Add(new KeyerPreviewViewport(bottomRightViewport, m_Keyer, Keyer.Display.Front));
        }

        void CreateKeyerRightViewports(VisualElement parent)
        {
            var rightViewport = CreateKeyerViewportContainer();
            parent.Add(rightViewport);
            m_KeyerPreviewVp.Add(new KeyerPreviewViewport(rightViewport, m_Keyer, Keyer.Display.Result));
        }

        void CreateKeyerViewports(VisualElement parent)
        {
            m_KeyerVpContainer = new TwoPaneSplitView(0, k_MinWidth, TwoPaneSplitViewOrientation.Horizontal);
            m_KeyerVpContainer.style.minWidth = 600;
            m_KeyerVpContainer.style.width = 600;

            CreateKeyerLeftViewports(m_KeyerVpContainer);
            CreateKeyerRightViewports(m_KeyerVpContainer);

            foreach (var kp in m_KeyerPreviewVp)
            {
                kp.TexturePreview.TransformChanged += PreviewTransformChanged;
                kp.TexturePreview.ModifierPressed += OnModifierPressed;
            }

            parent.Add(m_KeyerVpContainer);
        }

        void CreateKeyerEditorContainer(VisualElement parent)
        {
            m_KeyerEditorContainer = new VisualElement();
            m_KeyerEditorContainer.style.flexDirection = FlexDirection.Column;
            m_KeyerEditorContainer.style.flexGrow = 1;
            m_KeyerEditorContainer.style.alignContent = Align.Stretch;
            m_KeyerEditorContainer.style.minWidth = k_MinWidth;

            parent.Add(m_KeyerEditorContainer);
        }

        public VisualElement CreateGUI()
        {

            m_TopContainer = new VisualElement();
            m_TopContainer.style.flexGrow = 1;
            m_TopContainer.style.flexDirection = FlexDirection.Column;
            var mainSplitView = new TwoPaneSplitView(1, k_MinWidth, TwoPaneSplitViewOrientation.Horizontal);
            mainSplitView.style.flexGrow = 1;
            m_TopContainer.Add(mainSplitView);

            CreateKeyerViewports(mainSplitView);
            CreateKeyerEditorContainer(mainSplitView);

            if (DisplayModifierInfo)
            {
                m_ModifierInfoBox = new HelpBox(k_PanZoomInfo, HelpBoxMessageType.Info);
                m_ModifierInfoBox.style.alignItems = Align.Center;
                m_ModifierInfoBox.style.height = 32;
                m_TopContainer.Add(m_ModifierInfoBox);
            }
            SetKeyer(m_Keyer);
            return m_TopContainer;
        }

        public void OnEnable()
        {
            // no-op
        }

        public void OnDisable()
        {
            foreach (var kp in m_KeyerPreviewVp)
            {
                kp.OnDisable();
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void SetKeyer(Keyer newKeyer)
        {
            m_Keyer = newKeyer;
            foreach (var kp in m_KeyerPreviewVp)
            {
                kp.KeyerComponentChange(newKeyer);
            }

            m_InspectorScrollView?.RemoveFromHierarchy();
            m_KeyerEditor?.RemoveFromHierarchy();

            if (newKeyer == null)
            {
                return;
            }

            m_InspectorScrollView = UIUtils.CreateKeyerInspector(m_Keyer, m_KeyerEditorContainer);
        }
    }
}
