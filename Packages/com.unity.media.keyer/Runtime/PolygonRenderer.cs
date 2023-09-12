using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Media.Keyer.Geometry;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    [Serializable]
    class PolygonRenderer : IDisposable
    {
        public struct Stats
        {
            public double TriangulateElapsedMs;
            public double ReadGeometryElapsedMs;
            public double RenderGeometryElapsedMs;
            public double RenderGeometryGpuElapsedMs;
        }

        readonly DoublyConnectedEdgeList m_DoublyConnectedEdgeList = new();
        readonly List<Vector2> m_Points = new();
        ComputeBuffer m_VerticesBuffer;
        ComputeBuffer m_IndicesBuffer;
        Material m_Material;

        CustomSampler m_TriangulateSampler;
        CustomSampler m_ReadGeometrySampler;
        CustomSampler m_RenderGeometrySampler;

        Stats m_Stats;

        public void Initialize()
        {
            // Cannot use ??= operator because our format tool breaks it into ?? = which is invalid.
            if (m_TriangulateSampler == null)
            {
                m_TriangulateSampler = CustomSampler.Create($"{nameof(PolygonRenderer)}-Triangulate");
            }

            if (m_ReadGeometrySampler == null)
            {
                m_ReadGeometrySampler = CustomSampler.Create($"{nameof(PolygonRenderer)}-ReadGeometry");
            }

            if (m_RenderGeometrySampler == null)
            {
                m_RenderGeometrySampler = CustomSampler.Create($"{nameof(PolygonRenderer)}-RenderGeometry", true);
            }
        }

        public void Dispose()
        {
            m_DoublyConnectedEdgeList.Dispose();

            Utilities.DeallocateIfNeeded(ref m_VerticesBuffer);
            Utilities.DeallocateIfNeeded(ref m_IndicesBuffer);
            Utilities.Destroy(m_Material);
        }

        public void Render(CommandBuffer cmd, List<Vector2> points)
        {
            if (points == null || points.Count < 3)
            {
                //throw new InvalidOperationException($"Cannot generate mask with less than 3 points.");
                return;
            }

            // TODO This constraint may be relaxed.
            if (Geometry.Utilities.HasSelfIntersection(points))
            {
                throw new InvalidOperationException($"Cannot generate mask with self intersecting polygon.");
            }

            m_Points.Clear();
            m_Points.AddRange(points);
            Geometry.Utilities.RemoveDuplicatesAndCollinear(m_Points, 1e-2f);

            var verticesCcw = new NativeArray<float2>(m_Points.Count, Allocator.Temp);
            for (var i = 0; i != m_Points.Count; ++i)
            {
                verticesCcw[i] = m_Points[i];
            }

            var order = Geometry.Utilities.GetOrder(verticesCcw);
            if (order == Order.ClockWise)
            {
                Utilities.Reverse(verticesCcw);
            }

            m_DoublyConnectedEdgeList.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);

            using (new CustomSamplerScope(m_TriangulateSampler))
            {
                Triangulate.Execute(m_DoublyConnectedEdgeList);
            }

            var vertices = default(NativeArray<float2>);
            var indices = default(NativeArray<int>);

            using (new CustomSamplerScope(m_ReadGeometrySampler))
            {
                m_DoublyConnectedEdgeList.ExtractTriangles(out vertices, out indices, Allocator.Temp);
            }

            Utilities.AllocateBufferIfNeeded(ref m_VerticesBuffer, vertices);
            Utilities.AllocateBufferIfNeeded(ref m_IndicesBuffer, indices);

            if (m_Material == null)
            {
                var shader = KeyerResources.GetInstance().Shaders.Solid;
                m_Material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            m_Material.SetBuffer(ShaderIDs._Vertices, m_VerticesBuffer);
            m_Material.SetBuffer(ShaderIDs._Indices, m_IndicesBuffer);

            cmd.BeginSample(m_RenderGeometrySampler);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, m_IndicesBuffer.count);
            cmd.EndSample(m_RenderGeometrySampler);
        }

        public bool ReadStats(out Stats stats)
        {
            var changed = false;
            changed |= Utilities.ReadElapsedMs(m_TriangulateSampler, ref m_Stats.TriangulateElapsedMs);
            changed |= Utilities.ReadElapsedMs(m_ReadGeometrySampler, ref m_Stats.ReadGeometryElapsedMs);
            changed |= Utilities.ReadElapsedMs(m_RenderGeometrySampler, ref m_Stats.RenderGeometryElapsedMs);
            changed |= Utilities.ReadGpuElapsedMs(m_RenderGeometrySampler, ref m_Stats.RenderGeometryGpuElapsedMs);

            stats = m_Stats;
            return changed;
        }
    }
}
