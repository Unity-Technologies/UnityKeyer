using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Media.Keyer
{
    // Doesn't do much but makes for more readable code.
    class DoubleRenderTexture : IDisposable
    {
        RenderTexture m_TargetIn;
        RenderTexture m_TargetOut;

        public RenderTexture In => m_TargetIn;
        public RenderTexture Out => m_TargetOut;

        public void AllocateIfNeeded(int width, int height, GraphicsFormat format)
        {
            Utilities.AllocateIfNeeded(ref m_TargetIn, width, height, format);
            Utilities.AllocateIfNeeded(ref m_TargetOut, width, height, format);
        }

        public void AllocateIfNeededForCompute(int width, int height, GraphicsFormat format)
        {
            Utilities.AllocateIfNeededForCompute(ref m_TargetIn, width, height, format);
            Utilities.AllocateIfNeededForCompute(ref m_TargetOut, width, height, format);
        }

        public void Dispose()
        {
            Utilities.DeallocateIfNeeded(ref m_TargetIn);
            Utilities.DeallocateIfNeeded(ref m_TargetOut);
        }

        public void Swap()
        {
            Utilities.Swap(ref m_TargetIn, ref m_TargetOut);
        }
    }
}
