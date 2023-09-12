using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    // Doesn't do much but makes for more readable code.
    class DoubleBuffer<T> : IDisposable where T : struct
    {
        ComputeBuffer m_BufferIn;
        ComputeBuffer m_BufferOut;

        public ComputeBuffer In => m_BufferIn;
        public ComputeBuffer Out => m_BufferOut;

        public void AllocateIfNeeded(int sizeIn, int sizeOut)
        {
            Utilities.AllocateBufferIfNeeded<T>(ref m_BufferIn, sizeIn);
            Utilities.AllocateBufferIfNeeded<T>(ref m_BufferOut, sizeOut);
        }

        public void AllocateIfNeeded(int size) => AllocateIfNeeded(size, size);

        public void Dispose()
        {
            Utilities.DeallocateIfNeeded(ref m_BufferIn);
            Utilities.DeallocateIfNeeded(ref m_BufferOut);
        }

        public void Swap()
        {
            Utilities.Swap(ref m_BufferIn, ref m_BufferOut);
        }
    }
}
