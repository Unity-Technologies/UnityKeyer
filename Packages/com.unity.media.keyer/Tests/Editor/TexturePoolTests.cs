using System;
using NUnit.Framework;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class TexturePoolTests
    {
        TexturePool m_TexturePool;

        [SetUp]
        public void Setup()
        {
            BufferFormatUtility.SetQuality(RenderingQuality.Standard);
            m_TexturePool = new TexturePool();
        }

        [TearDown]
        public void TearDown()
        {
            m_TexturePool.Dispose();
        }

        [Test]
        public void AssignZeroSizeThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => m_TexturePool.ResizeIfNeeded(0, 0));
        }

        [Test]
        public void GetWithInvalidBufferFormatThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => m_TexturePool.Get(BufferFormat.None));
        }

        [Test]
        public void ReleasedTexturesArePooled()
        {
            m_TexturePool.ResizeIfNeeded(128, 128);
            var tex0 = m_TexturePool.Get(BufferFormat.R);
            var tex1 = m_TexturePool.Get(BufferFormat.R);
            Assert.IsTrue(m_TexturePool.AllocatedCount == 2);
            Assert.IsTrue(m_TexturePool.GetPooledCount() == 0);
            m_TexturePool.Release(tex0);
            m_TexturePool.Release(tex1);
            Assert.IsTrue(m_TexturePool.AllocatedCount == 2);
            Assert.IsTrue(m_TexturePool.GetPooledCount() == 2);
        }

        [Test]
        public void DisposeClearsActiveTextures()
        {
            m_TexturePool.ResizeIfNeeded(128, 128);
            var _ = m_TexturePool.Get(BufferFormat.R);
            var _1 = m_TexturePool.Get(BufferFormat.R);
            Assert.IsTrue(m_TexturePool.AllocatedCount == 2);
            m_TexturePool.Dispose();
            Assert.IsTrue(m_TexturePool.AllocatedCount == 0);
            Assert.IsTrue(m_TexturePool.GetPooledCount() == 0);
        }
    }
}
