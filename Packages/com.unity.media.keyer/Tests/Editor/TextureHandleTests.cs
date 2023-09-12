using System;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class TextureHandleTests
    {
        TexturePool m_TexturePool;
        TextureHandles m_Handles;

        [SetUp]
        public void Setup()
        {
            BufferFormatUtility.SetQuality(RenderingQuality.Standard);
            m_TexturePool = new TexturePool();
            m_TexturePool.ResizeIfNeeded(128, 128);
            m_Handles = new TextureHandles(m_TexturePool);
        }

        [TearDown]
        public void TearDown()
        {
            m_Handles.Dispose();
            m_TexturePool.Dispose();
        }

        [Test]
        public void DefaultHandleIsInvalid()
        {
            var handle = default(TextureHandle);
            Assert.IsFalse(handle.IsValid());
        }

        [Test]
        public void ReadingInvalidTransientThrows()
        {
            var invalidHandle = default(TextureHandle);
            Assert.Throws<InvalidOperationException>(
                () => m_Handles.CreateReadTransient(invalidHandle));
        }

        [Test]
        public void CreateWriteTransientWithBufferFormatNoneThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => m_Handles.CreateWriteTransient(BufferFormat.None, "test"));
        }

        [Test]
        public void CreateTempTransientWithBufferFormatNoneThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => m_Handles.CreateTempTransient(BufferFormat.None, "test"));
        }

        [Test]
        public void CreateReadTransientFromTempThrows()
        {
            var temp = m_Handles.CreateTempTransient(BufferFormat.R, "test");
            Assert.Throws<InvalidOperationException>(
                () => m_Handles.CreateReadTransient(temp));
        }

        [Test]
        public void UnreadWriteTransientIsUnused()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            Assert.IsTrue(m_Handles.IsUnusedTransient(source));
        }

        [Test]
        public void ReadWriteTransientIsUsed()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var _ = m_Handles.CreateReadTransient(source);
            Assert.IsFalse(m_Handles.IsUnusedTransient(source));
        }

        [Test]
        public void ReadWriteTransientIsUnusedAfterDeletion()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest = m_Handles.CreateReadTransient(source);
            Assert.IsFalse(m_Handles.IsUnusedTransient(source));
            m_Handles.DestroyIfTransient(dest);
            Assert.IsTrue(m_Handles.IsUnusedTransient(source));
        }

        [Test]
        public void ReadTransientIsInvalidAfterWriteDeletion()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest = m_Handles.CreateReadTransient(source);
            m_Handles.DestroyIfTransient(source);
            Assert.IsFalse(dest.IsValid());
        }

        [Test]
        public void ReadWriteConsumedTransientIsReleased()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest = m_Handles.CreateReadTransient(source);

            m_Handles.Reset();
            // Dereferences.
            var _ = (Texture)source;
            var _1 = (Texture)dest;
            Assert.IsTrue(m_Handles.ReleaseConsumedTransients() == 1);
        }

        [Test]
        public void TempConsumedTransientIsReleased()
        {
            var temp = m_Handles.CreateTempTransient(BufferFormat.R, "test");

            m_Handles.Reset();
            // Dereferences.
            var _ = (Texture)temp;
            Assert.IsTrue(m_Handles.ReleaseConsumedTransients() == 1);
        }

        [Test]
        public void TransientTexturesAreRecycled()
        {
            var source0 = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest0 = m_Handles.CreateReadTransient(source0);
            var source1 = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest1 = m_Handles.CreateReadTransient(source1);

            m_TexturePool.Dispose();
            m_Handles.Reset();

            // Dereferences.
            var _ = (Texture)source0;
            var _1 = (Texture)dest0;
            Assert.IsTrue(m_Handles.ReleaseConsumedTransients() == 1);

            // Dereferences.
            var _2 = (Texture)source1;
            var _3 = (Texture)dest1;
            Assert.IsTrue(m_Handles.ReleaseConsumedTransients() == 1);

            Assert.IsTrue(m_TexturePool.AllocatedCount == 1);
            Assert.IsTrue(m_TexturePool.GetPooledCount() == 1);
        }

        [Test]
        public void DisposedHandlesAreInvalidated()
        {
            var source = m_Handles.CreateWriteTransient(BufferFormat.R, "test");
            var dest = m_Handles.CreateReadTransient(source);
            var temp = m_Handles.CreateTempTransient(BufferFormat.R, "temp");
            Assert.IsTrue(source.IsValid());
            Assert.IsTrue(dest.IsValid());
            Assert.IsTrue(temp.IsValid());
            m_Handles.Dispose();
            Assert.IsFalse(source.IsValid());
            Assert.IsFalse(dest.IsValid());
            Assert.IsFalse(temp.IsValid());
        }
    }
}
