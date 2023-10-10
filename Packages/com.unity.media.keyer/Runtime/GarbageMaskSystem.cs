using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    class GarbageMaskSystem : IDisposable
    {
        public interface IRenderer : IDisposable
        {
            void Initialize();
            void RenderPolygon(CommandBuffer cmd, List<Vector2> points, RenderTexture target);
            void RenderSdf(CommandBuffer cmd, Texture source, RenderTexture target, SdfQuality quality, int distance);
        }

        class Renderer : IRenderer
        {
            public struct Stats
            {
                public double ElapsedMs;
                public double GpuElapsedMs;
            }

            readonly SignedDistanceField m_SignedDistanceField = new();
            readonly PolygonRenderer m_PolygonRenderer = new();

            CustomSampler m_SdfSampler;
            Stats m_SdfStats;

            public void Initialize()
            {
                // Cannot use ??= operator because our format tool breaks it into ?? = which is invalid.
                if (m_SdfSampler == null)
                {
                    m_SdfSampler = CustomSampler.Create("SDF Render", true);
                }

                m_SignedDistanceField.Initialize();
                m_PolygonRenderer.Initialize();
            }

            public void Dispose()
            {
                m_SignedDistanceField.Dispose();
                m_PolygonRenderer.Dispose();
            }

            public void RenderPolygon(CommandBuffer cmd, List<Vector2> points, RenderTexture target)
            {
                cmd.SetRenderTarget(target);
                cmd.ClearRenderTarget(false, true, Color.clear);
                m_PolygonRenderer.Render(cmd, points);
            }

            public void RenderSdf(CommandBuffer cmd, Texture source, RenderTexture target, SdfQuality quality, int distance)
            {
                var sdfSettings = SignedDistanceField.Settings.Get(quality, distance);
                cmd.BeginSample(m_SdfSampler);
                m_SignedDistanceField.Execute(cmd, source, target, sdfSettings);
                cmd.EndSample(m_SdfSampler);
            }

            public bool ReadSdfStats(out Stats stats)
            {
                var changed = false;
                changed |= Utilities.ReadElapsedMs(m_SdfSampler, ref m_SdfStats.ElapsedMs);
                changed |= Utilities.ReadGpuElapsedMs(m_SdfSampler, ref m_SdfStats.GpuElapsedMs);

                stats = m_SdfStats;
                return changed;
            }
        }

        // Must not exceed SignedDistanceField.Settings.k_MaxScale.
        const int k_MaxSdfDistance = 64;
        const int k_MinSdfDistance = 4;

        IRenderer m_Renderer;
        RenderTexture m_PolygonTarget;
        RenderTexture m_SdfTarget;
        int m_LastSdfHash;

        public void Initialize()
        {
            m_Renderer = new Renderer();
            m_Renderer.Initialize();
        }

        public void Initialize(IRenderer renderer)
        {
            m_Renderer = renderer;
            m_Renderer.Initialize();
        }

        public void Dispose()
        {
            m_Renderer.Dispose();
            Utilities.DeallocateIfNeeded(ref m_PolygonTarget);
            Utilities.DeallocateIfNeeded(ref m_SdfTarget);
        }

        public Texture Update(CommandBuffer cmd, GarbageMask garbageMask, Vector2Int size, ref bool renderPolygon)
        {
            return Update(cmd, garbageMask, size, ref renderPolygon, out _);
        }

        public Texture Update(CommandBuffer cmd, GarbageMask garbageMask, Vector2Int size, ref bool renderPolygon, out bool renderSdf)
        {
            renderSdf = false;

            // Simplest case.
            if (garbageMask.Mode == GarbageMaskMode.Texture && !garbageMask.SdfEnabled)
            {
                return garbageMask.Texture;
            }

            var sdfSource = garbageMask.Texture;

            if (garbageMask.Mode == GarbageMaskMode.Polygon)
            {
                var points = garbageMask.Points;
                if (points == null || points.Count < 3)
                {
                    return null;
                }

                // We handle dimension changes internally.
                if (!renderPolygon)
                {
                    if (m_PolygonTarget == null)
                    {
                        renderPolygon = true;
                    }
                    else
                    {
                        renderPolygon |= m_PolygonTarget.width != size.x;
                        renderPolygon |= m_PolygonTarget.height != size.y;
                    }
                }

                // We could track geometry changes here but it would be slower as we'd need to traverse it.
                if (renderPolygon)
                {
                    Utilities.AllocateIfNeededForCompute(ref m_PolygonTarget, size.x, size.y, GraphicsFormat.R8_UNorm);
                    m_Renderer.RenderPolygon(cmd, points, m_PolygonTarget);
                    renderPolygon = false;

                    // Mask has changed, SDF needs update.
                    renderSdf = true;
                }

                sdfSource = m_PolygonTarget;
            }

            if (!garbageMask.SdfEnabled)
            {
                m_LastSdfHash = 0;
                return sdfSource;
            }

            if (sdfSource == null)
            {
                garbageMask.SdfEnabled = false;
                throw new InvalidOperationException("Sdf is enabled but no mask texture is assigned nor is the generator enabled.");
            }

            renderSdf |= Utilities.AllocateIfNeededForCompute(ref m_SdfTarget, sdfSource.width, sdfSource.height, GraphicsFormat.R8_UNorm);

            var distance = math.clamp(garbageMask.SdfDistance, k_MinSdfDistance, k_MaxSdfDistance);
            var quality = garbageMask.SdfQuality;

            // Change detection based on source and settings.
            var sdfHash = sdfSource.GetHashCode();
            sdfHash = (sdfHash * 397) ^ distance;
            sdfHash = (sdfHash * 397) ^ (int)quality;
            renderSdf |= sdfHash != m_LastSdfHash;
            m_LastSdfHash = sdfHash;

            if (renderSdf)
            {
                m_Renderer.RenderSdf(cmd, sdfSource, m_SdfTarget, quality, distance);
            }

            return m_SdfTarget;
        }
    }
}
