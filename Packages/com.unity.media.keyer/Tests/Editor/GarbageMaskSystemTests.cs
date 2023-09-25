using System.Collections.Generic;
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using UnityEngine.Rendering;
using Assert = UnityEngine.Assertions.Assert;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class GarbageMaskSystemTests
    {
        readonly GarbageMaskSystem.IRenderer m_Renderer = Substitute.For<GarbageMaskSystem.IRenderer>();
        readonly GarbageMaskSystem m_GarbageMaskSystem = new();
        readonly GarbageMask m_GarbageMask = new();

        [SetUp]
        public void Setup()
        {
            m_Renderer.ClearReceivedCalls();
            m_GarbageMaskSystem.Initialize(m_Renderer);
            m_GarbageMask.Texture = null;
            m_GarbageMask.SdfDistance = 32;
            m_GarbageMask.Mode = GarbageMaskMode.Texture;
            m_GarbageMask.SdfEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            m_GarbageMaskSystem.Dispose();
        }

        [Test]
        public void ForwardsAssignedTexture()
        {
            var cmd = CommandBufferPool.Get();

            m_GarbageMask.Texture = Texture2D.blackTexture;
            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsTrue(result == Texture2D.blackTexture);

            CheckRendererCalls(false, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void ReturnsNullIfGeometryIsInvalid()
        {
            var cmd = CommandBufferPool.Get();

            // Only 2 points make the geometry invalid.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNull(result);

            CheckRendererCalls(false, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void RendersGeometry()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;

            var renderPolygon = true;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void RendersSdfWithTexture()
        {
            var cmd = CommandBufferPool.Get();

            m_GarbageMask.Mode = GarbageMaskMode.Texture;
            m_GarbageMask.SdfEnabled = true;
            m_GarbageMask.Texture = Texture2D.blackTexture;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, true);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void RendersPolygonAndSdf()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;
            m_GarbageMask.SdfEnabled = true;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, true);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void DoesNotRenderSdfWithTextureTwice()
        {
            var cmd = CommandBufferPool.Get();

            m_GarbageMask.Mode = GarbageMaskMode.Texture;
            m_GarbageMask.SdfEnabled = true;
            m_GarbageMask.Texture = Texture2D.blackTexture;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, true);

            m_Renderer.ClearReceivedCalls();

            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void RendersSdfWhenSettingsChange()
        {
            var cmd = CommandBufferPool.Get();

            m_GarbageMask.Mode = GarbageMaskMode.Texture;
            m_GarbageMask.SdfEnabled = true;
            m_GarbageMask.Texture = Texture2D.blackTexture;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, true);

            m_Renderer.ClearReceivedCalls();

            m_GarbageMask.SdfDistance += 1;

            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, true);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void DoesNotRenderSdfWithGeometryTwice()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;
            m_GarbageMask.SdfEnabled = true;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, true);

            m_Renderer.ClearReceivedCalls();

            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void DoesNotRenderGeometryTwice()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;
            m_GarbageMask.SdfEnabled = false;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, false);

            m_Renderer.ClearReceivedCalls();

            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void RendersGeometryWhenForced()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;
            m_GarbageMask.SdfEnabled = false;

            var renderPolygon = true;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, false);

            m_Renderer.ClearReceivedCalls();

            renderPolygon = true;
            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, false);

            CommandBufferPool.Release(cmd);
        }

        [Test]
        public void MaskSwapInvalidatesSdf()
        {
            var cmd = CommandBufferPool.Get();

            // Triangle geometry.
            m_GarbageMask.Points.Clear();
            m_GarbageMask.Points.Add(Vector2.down);
            m_GarbageMask.Points.Add(Vector2.right);
            m_GarbageMask.Points.Add(Vector2.left);
            m_GarbageMask.Mode = GarbageMaskMode.Polygon;
            m_GarbageMask.SdfEnabled = true;

            var renderPolygon = false;
            var result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(true, true);

            m_Renderer.ClearReceivedCalls();

            m_GarbageMask.Mode = GarbageMaskMode.Texture;
            m_GarbageMask.Texture = Texture2D.blackTexture;

            result = m_GarbageMaskSystem.Update(cmd, m_GarbageMask, Vector2Int.one, ref renderPolygon);

            Assert.IsNotNull(result);

            CheckRendererCalls(false, true);

            CommandBufferPool.Release(cmd);
        }

        void CheckRendererCalls(bool polygon, bool sdf)
        {
            if (polygon)
            {
                m_Renderer.ReceivedWithAnyArgs().RenderPolygon(
                    Arg.Any<CommandBuffer>(),
                    Arg.Any<List<Vector2>>(), Arg.Any<RenderTexture>());
            }
            else
            {
                m_Renderer.DidNotReceiveWithAnyArgs().RenderPolygon(
                    Arg.Any<CommandBuffer>(),
                    Arg.Any<List<Vector2>>(), Arg.Any<RenderTexture>());
            }

            if (sdf)
            {
                m_Renderer.ReceivedWithAnyArgs().RenderSdf(
                    Arg.Any<CommandBuffer>(),
                    Arg.Any<Texture>(), Arg.Any<RenderTexture>(),
                    Arg.Any<SdfQuality>(), Arg.Any<int>());
            }
            else
            {
                m_Renderer.DidNotReceiveWithAnyArgs().RenderSdf(
                    Arg.Any<CommandBuffer>(),
                    Arg.Any<Texture>(), Arg.Any<RenderTexture>(),
                    Arg.Any<SdfQuality>(), Arg.Any<int>());
            }
        }
    }
}
