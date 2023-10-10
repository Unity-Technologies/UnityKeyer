using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class CopyPassTests
    {
        static readonly Stack<RenderTexture> s_RenderTextures = new();

        TextureHandles m_TextureHandles;
        TexturePool m_TexturePool;

        [SetUp]
        public void Setup()
        {
            m_TexturePool = new TexturePool();
            m_TexturePool.ResizeIfNeeded(128, 128);
            m_TextureHandles = new TextureHandles(m_TexturePool);
        }

        [TearDown]
        public void TearDown()
        {
            while (s_RenderTextures.Count > 0)
            {
                var rt = s_RenderTextures.Pop();
                rt.Release();
            }

            m_TextureHandles.Dispose();
            m_TexturePool.Dispose();
        }

        [TestCase(GraphicsFormat.R8G8_UNorm, GraphicsFormat.R8G8B8A8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.R8_UNorm)]
        public void UnsupportedFormatCombinationThrows(GraphicsFormat inputFormat, GraphicsFormat outputFormat)
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var _ = CreateCopyPass(m_TextureHandles, inputFormat, outputFormat);
            });
        }

        [TestCase(GraphicsFormat.R8_UNorm, GraphicsFormat.R8G8B8A8_UNorm)]
        [TestCase(GraphicsFormat.R8G8B8A8_UNorm, GraphicsFormat.R8G8B8A8_UNorm)]
        public void SupportedFormatCombinationDoesNotThrow(GraphicsFormat inputFormat, GraphicsFormat outputFormat)
        {
            Assert.DoesNotThrow(() =>
            {
                var _ = CreateCopyPass(m_TextureHandles, inputFormat, outputFormat);
            });
        }

        static CopyPass CreateCopyPass(TextureHandles handles, GraphicsFormat inputFormat, GraphicsFormat outputFormat)
        {
            var input = new RenderTexture(128, 128, 0, inputFormat);
            var output = new RenderTexture(128, 128, 0, outputFormat);
            s_RenderTextures.Push(input);
            s_RenderTextures.Push(output);

            return new CopyPass(new CopyPassData
            {
                Input = handles.FromTexture(input),
                Output = handles.FromTexture(output)
            });
        }
    }
}
