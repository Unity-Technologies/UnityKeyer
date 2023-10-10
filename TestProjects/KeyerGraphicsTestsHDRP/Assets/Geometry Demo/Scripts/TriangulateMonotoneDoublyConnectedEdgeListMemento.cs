using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    class TriangulateMonotoneDoublyConnectedEdgeListMemento : IDoublyConnectedEdgeList
    {
        public struct Snapshot
        {
            // Index of the first vertex on the stack.
            public int StackIndex;

            // Number of items in the stack;
            public int StackCount;
            public float2 Vertex;

            // Index of the last edge added up to this point.
            public int EdgeCount;
            public float2x2 PendingEdgeA;
            public float2x2 PendingEdgeB;
        }

        // We record stack snapshots in one collection.
        readonly List<float2> m_StackSnapShots = new();
        readonly List<float2x2> m_AddedEdges = new();
        readonly List<Snapshot> m_Snapshots = new();

        DoublyConnectedEdgeList m_Dcel;
        TriangulateMonotone.VertexStack m_VerticesStack;
        bool m_IsValid;

        public IReadOnlyCollection<float2> StackSnapShots => m_StackSnapShots;
        public IReadOnlyCollection<float2x2> AddedEdges => m_AddedEdges;
        public IReadOnlyCollection<Snapshot> Snapshots => m_Snapshots;

        public void Initialize(DoublyConnectedEdgeList dcel, TriangulateMonotone.VertexStack verticesStack)
        {
            m_StackSnapShots.Clear();
            m_AddedEdges.Clear();
            m_Snapshots.Clear();
            m_Dcel = dcel;
            m_VerticesStack = verticesStack;
            m_IsValid = true;
        }

        public DoublyConnectedEdgeList.VerticesIterator GetVerticesIterator()
        {
            return m_Dcel.GetVerticesIterator();
        }

        // Here we conform to the interface but do not expect the method to be invoked.
        public void SplitFace(DoublyConnectedEdgeList.HalfEdge edge, DoublyConnectedEdgeList.Vertex vertex)
        {
            throw new NotImplementedException();
        }

        public void SplitFace(DoublyConnectedEdgeList.HalfEdge left, DoublyConnectedEdgeList.HalfEdge right, EdgeAssign edgeAssign, DoublyConnectedEdgeList.Vertex vertex)
        {
            if (!m_IsValid)
            {
                return;
            }

            // Register two snapshots, before and after adding the edge.
            var beforeSnapshot = GetSnapshot();
            beforeSnapshot.Vertex = vertex;
            beforeSnapshot.EdgeCount = m_AddedEdges.Count;
            beforeSnapshot.PendingEdgeA = left;
            beforeSnapshot.PendingEdgeB = right;
            m_Snapshots.Add(beforeSnapshot);

            try
            {
                m_Dcel.SplitFace(left, right, edgeAssign, out var newEdge);
                m_AddedEdges.Add(newEdge);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"Triangulation error: {e.Message}");
                m_IsValid = false;
            }

            if (m_IsValid)
            {
                var afterSnapshot = GetSnapshot();
                afterSnapshot.Vertex = vertex;
                afterSnapshot.EdgeCount = m_AddedEdges.Count;
                m_Snapshots.Add(afterSnapshot);
            }
        }

        // Handles stack, edges and side capture.
        Snapshot GetSnapshot()
        {
            var startIndex = m_StackSnapShots.Count;
            foreach (var vertex in m_VerticesStack.Vertices)
            {
                m_StackSnapShots.Add(vertex);
            }

            var length = m_StackSnapShots.Count - startIndex;

            return new Snapshot
            {
                StackIndex = startIndex,
                StackCount = length,
                EdgeCount = m_AddedEdges.Count
            };
        }
    }
}
