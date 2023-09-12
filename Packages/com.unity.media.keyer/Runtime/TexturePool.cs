using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Keyer
{
    class TexturePool : IDisposable
    {
        readonly Dictionary<BufferFormat, Stack<RenderTexture>> m_Textures = new();

        int m_Width;
        int m_Height;
        int m_AllocatedCount;
        bool m_DimensionsAreValid;

        public int AllocatedCount => m_AllocatedCount;

        public int GetPooledCount()
        {
            var count = 0;
            foreach (var entry in m_Textures)
            {
                count += entry.Value.Count;
            }

            return count;
        }

        public TexturePool()
        {
            m_Textures.Add(BufferFormat.RGBA, new Stack<RenderTexture>());
            m_Textures.Add(BufferFormat.R, new Stack<RenderTexture>());
        }

        public void Dispose()
        {
            m_AllocatedCount = 0;
            foreach (var stack in m_Textures.Values)
            {
                while (stack.Count > 0)
                {
                    Utilities.DeallocateIfNeeded(stack.Pop());
                }
            }
        }

        public void ResizeIfNeeded(int width, int height)
        {
            var dimensionChanged = m_Width != width || m_Height != height;

            m_Width = width;
            m_Height = height;
            CheckDimensions();

            // We simply flush. Reallocating the targets would entail flushing them as a first step anyway.
            if (dimensionChanged)
            {
                Dispose();
            }
        }

        public void Release(RenderTexture texture)
        {
            m_Textures[BufferFormatUtility.GetBufferFormat(texture.graphicsFormat)].Push(texture);
        }

        public RenderTexture Get(BufferFormat format)
        {
            if (format == BufferFormat.None)
            {
                throw new InvalidOperationException(
                    $"{nameof(BufferFormat)} {nameof(BufferFormat.None)} is invalid.");
            }

            CheckDimensions();

            var stack = m_Textures[format];
            if (stack.Count > 0)
            {
                return stack.Pop();
            }

            var rt = default(RenderTexture);

            // Note that all textures have read/write enabled.
            Utilities.AllocateIfNeededForCompute(ref rt, m_Width, m_Height, BufferFormatUtility.GetGraphicsFormat(format));
            m_AllocatedCount++;
            return rt;
        }

        void CheckDimensions()
        {
            if (m_Width < 1 || m_Height < 1)
            {
                throw new InvalidOperationException(
                    $"{nameof(TexturePool)}, Invalid dimensions {m_Width}x{m_Height}.");
            }
        }
    }
}
