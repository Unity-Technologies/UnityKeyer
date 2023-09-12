using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    class SplitToMonotoneDoublyConnectedEdgeListMemento : IDoublyConnectedEdgeList
    {
        public struct Snapshot
        {
            public int EdgeCount;
            public int EdgeAndHelperIndex;
            public int EdgeAndHelperCount;
            public float SweepLineY;
        }

        public struct EdgeAndHelper
        {
            public float2x2 Edge;
            public float2x2 Helper;
        }

        // For visualization.
        readonly List<float2x2> m_AddedEdges = new();
        readonly List<Snapshot> m_Snapshots = new();
        readonly List<EdgeAndHelper> m_EdgesAndHelpers = new();

        DoublyConnectedEdgeList m_Dcel;
        SplitToMonotone.SweepStatus m_SweepStatus;
        bool m_IsValid;

        public IReadOnlyCollection<float2x2> AddedEdges => m_AddedEdges;
        public IReadOnlyCollection<Snapshot> Snapshots => m_Snapshots;
        public IReadOnlyCollection<EdgeAndHelper> EdgesAndHelpers => m_EdgesAndHelpers;

        public void Initialize(DoublyConnectedEdgeList dcel, SplitToMonotone.SweepStatus sweepStatus)
        {
            m_AddedEdges.Clear();
            m_Snapshots.Clear();
            m_EdgesAndHelpers.Clear();
            m_IsValid = true;
            m_Dcel = dcel;
            m_SweepStatus = sweepStatus;
        }

        public DoublyConnectedEdgeList.VerticesIterator GetVerticesIterator()
        {
            return m_Dcel.GetVerticesIterator();
        }

        public void SplitFace(DoublyConnectedEdgeList.HalfEdge edge, DoublyConnectedEdgeList.Vertex vertex)
        {
            if (!m_IsValid)
            {
                return;
            }

            m_Snapshots.Add(GetSnapshot());

            // We deliberately handle errors as this component is meant for debugging.
            try
            {
                // We will not reassign the half-edges of vertices as we need to preserve the topology to find helper edges.
                // Therefore we pass an half-edge and a vertex.
                // We have no guarantees that incident half-edges are on the same face.
                m_Dcel.SplitFace(edge, vertex, EdgeAssign.None, out var newEdge);
                m_AddedEdges.Add(newEdge);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"Triangulation error: {e.Message}");
                m_IsValid = false;
            }

            if (m_IsValid)
            {
                m_Snapshots.Add(GetSnapshot());
            }
        }

        // Here we conform to the interface but do not expect the method to be invoked.
        public void SplitFace(DoublyConnectedEdgeList.HalfEdge left, DoublyConnectedEdgeList.HalfEdge right, EdgeAssign edgeAssign, DoublyConnectedEdgeList.Vertex vertex)
        {
            throw new NotImplementedException();
        }

        // Handles stack, edges and side capture.
        Snapshot GetSnapshot()
        {
            var startIndex = m_EdgesAndHelpers.Count;
            foreach (var edgeAndHelper in m_SweepStatus.EdgesAndHelpers())
            {
                m_EdgesAndHelpers.Add(new EdgeAndHelper
                {
                    Edge = edgeAndHelper.Key,
                    Helper = edgeAndHelper.Value.GetIncidentEdge()
                });
            }

            var length = m_EdgesAndHelpers.Count - startIndex;

            return new Snapshot
            {
                EdgeAndHelperIndex = startIndex,
                EdgeAndHelperCount = length,
                SweepLineY = m_SweepStatus.SweepLineY,
                EdgeCount = m_AddedEdges.Count
            };
        }
    }
}
