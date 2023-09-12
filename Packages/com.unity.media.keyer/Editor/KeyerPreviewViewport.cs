using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    class KeyerPreviewViewport : IKeyerAccess
    {
        const int k_DefaultRenderTextureWidth = 1920;
        const int k_DefaultRenderTextureHeight = 1080;
        TexturePreview m_TexturePreview;
        DropdownField m_DisplayField;
        VisualElement m_Parent;
        RenderTexture m_PreviewRT;
        Keyer m_Keyer;
        Keyer.Display m_CurrentDisplay;
        readonly DisplayDropdownWrapper m_DropdownWrapper;

        Keyer IKeyerAccess.GetKeyer() => m_Keyer;

        Action m_Changed = delegate { };

        event Action IKeyerAccess.Changed
        {
            add => m_Changed += value;
            remove => m_Changed -= value;
        }

        public KeyerPreviewViewport(VisualElement parent, Keyer keyer, Keyer.Display display)
        {
            m_Keyer = keyer;
            m_TexturePreview = new TexturePreview();
            m_TexturePreview.name = display.ToString();
            m_DisplayField = new DropdownField();
            m_DisplayField.userData = m_TexturePreview;
            m_DisplayField.AddToClassList("ui-display-field-border");

            parent.Add(m_DisplayField);
            parent.Add(m_TexturePreview);

            m_DropdownWrapper = new DisplayDropdownWrapper(m_DisplayField, this, display, OnDisplayChanged);
            RegisterKeyerChangeIfNeeded();
        }

        public TexturePreview TexturePreview => m_TexturePreview;

        public void KeyerComponentChange(Keyer newKeyer)
        {
            UnregisterKeyerChangeIfNeeded();
            UnregisterPreviewRTIfNeeded(m_Keyer, m_CurrentDisplay, m_PreviewRT);
            m_CurrentDisplay = m_DropdownWrapper.GetValue();
            m_Keyer = newKeyer;
            if (m_Keyer == null)
            {
                Utilities.DeallocateIfNeeded(m_PreviewRT);
                m_TexturePreview.Texture = null;
                return;
            }

            AllocatePreviewRT();
            RegisterPreviewRT(m_Keyer, m_CurrentDisplay, m_PreviewRT);
            RegisterKeyerChangeIfNeeded();

            // Force a redrawing the keyer graph to update the previews
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void RegisterKeyerChangeIfNeeded()
        {
            if (m_Keyer != null)
            {
                var access = (IKeyerAccess)m_Keyer;
                access.Changed += m_Changed.Invoke;
            }
        }

        void UnregisterKeyerChangeIfNeeded()
        {
            if (m_Keyer != null)
            {
                var access = (IKeyerAccess)m_Keyer;
                access.Changed -= m_Changed.Invoke;
            }
        }

        void AllocatePreviewRT()
        {
            var w = k_DefaultRenderTextureWidth;
            var h = k_DefaultRenderTextureHeight;
            if (m_Keyer != null && m_Keyer.Foreground != null)
            {
                w = m_Keyer.Foreground.width;
                h = m_Keyer.Foreground.height;
            }

            Utilities.AllocateIfNeeded(ref m_PreviewRT, w, h, 0, true);
        }

        void OnDisplayChanged(Keyer.Display display)
        {
            UnregisterPreviewRTIfNeeded(m_Keyer, m_CurrentDisplay, m_PreviewRT);
            m_CurrentDisplay = display;

            if (m_Keyer == null)
            {
                return;
            }

            AllocatePreviewRT();
            m_PreviewRT.name = m_CurrentDisplay.ToString();
            RegisterPreviewRT(m_Keyer, m_CurrentDisplay, m_PreviewRT);

            // Force a redrawing the keyer graph to update the previews
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void UnregisterPreviewRTIfNeeded(Keyer keyer, Keyer.Display display, RenderTexture rt)
        {
            if (keyer != null)
            {
                keyer.RemoveCapture(display, rt);
                m_TexturePreview.Texture = null;
                m_TexturePreview.Transparent = false;
            }
        }

        void RegisterPreviewRT(Keyer keyer, Keyer.Display display, RenderTexture rt)
        {
            keyer.AddCapture(display, rt);
            m_TexturePreview.Texture = rt;
            m_TexturePreview.Transparent = (display == Keyer.Display.Result);
        }

        public void OnDisable()
        {
            UnregisterPreviewRTIfNeeded(m_Keyer, m_CurrentDisplay, m_PreviewRT);
            Utilities.DeallocateIfNeeded(m_PreviewRT);
        }
    }
}
