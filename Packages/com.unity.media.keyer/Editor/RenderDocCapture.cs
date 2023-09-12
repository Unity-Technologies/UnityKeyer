using System;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.Media.Keyer.Editor
{
    class RenderDocCaptureScope : EditorBridge.IRenderDocCaptureScope
    {
        // We assume we want to capture the Game View.
        static readonly Type k_GameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");

        EditorWindow m_Window;

        public RenderDocCaptureScope()
        {
            m_Window = EditorWindow.GetWindow(k_GameViewType);

            if (IsValid())
            {
                RenderDoc.BeginCaptureRenderDoc(m_Window);
            }
        }

        public void Dispose()
        {
            if (IsValid())
            {
                RenderDoc.EndCaptureRenderDoc(m_Window);
            }
        }

        bool IsValid() => m_Window != null &&
        RenderDoc.IsLoaded() &&
        RenderDoc.IsSupported();
    }
}
