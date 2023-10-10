using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Keyer.Editor
{
    class EditorBridgeImpl : EditorBridge.IEditorBridgeImpl
    {
        static EditorBridgeImpl s_Instance = new();

        [InitializeOnLoadMethod]
        static void Initialize() => EditorBridge.SetImpl(s_Instance);

        public EditorBridge.IRenderDocCaptureScope CreateRenderDocCaptureScope() => new RenderDocCaptureScope();
        public void LoadResources() => ResourcesLoader.LoadResourcesMenu();
    }
}
