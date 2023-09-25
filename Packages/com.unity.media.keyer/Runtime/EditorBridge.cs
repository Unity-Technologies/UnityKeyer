using System;

namespace Unity.Media.Keyer
{
    static class EditorBridge
    {
        public interface IRenderDocCaptureScope : IDisposable { }

        class NullRenderDocCaptureScope : IRenderDocCaptureScope
        {
            static NullRenderDocCaptureScope s_Instance = new();

            public static IRenderDocCaptureScope Instance => s_Instance;

            public void Dispose() { }
        }

        public interface IEditorBridgeImpl
        {
            IRenderDocCaptureScope CreateRenderDocCaptureScope();
            void LoadResources();
        }

        static IEditorBridgeImpl s_Impl;

        public static void SetImpl(IEditorBridgeImpl impl) => s_Impl = impl;

        public static IRenderDocCaptureScope CreateRenderDocCaptureScope()
        {
            return s_Impl != null ? s_Impl.CreateRenderDocCaptureScope() : NullRenderDocCaptureScope.Instance;
        }

        public static void LoadResources()
        {
            s_Impl?.LoadResources();
        }
    }
}
