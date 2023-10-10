using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Media.Keyer.Geometry
{
    partial class DoublyConnectedEdgeList
    {
        // Used temporarily, saves allocations.
        static readonly Stack<Vertex> s_VerticesStack = new();
        static readonly Queue<Vertex> s_VerticesQueue = new();

        static void GetTopAndBottomVertices(Face face, out HalfEdge top, out HalfEdge bottom)
        {
            // The iterator will start at the outer component.
            var origin = face.GetOuterComponent();
            top = origin;
            bottom = origin;
            var comparer = new VertexSweepComparer();

            foreach (var edge in new HalfEdgesIterator(face))
            {
                var vertex = edge.GetOrigin();

                if (comparer.Compare(top.GetOrigin(), vertex) > 0)
                {
                    top = edge;
                }
                else if (comparer.Compare(vertex, bottom.GetOrigin()) > 0)
                {
                    bottom = edge;
                }
            }
        }

        public static void EnsureVerticesIncidentEdgesAreOnFace(Face face)
        {
            foreach (var edge in new HalfEdgesIterator(face))
            {
                edge.GetOrigin().SetIncidentEdge(edge);
            }
        }

        public static void LabelChains(Face face)
        {
            GetTopAndBottomVertices(face, out var top, out var bottom);

            foreach (var edge in new LeftChainIterator(top, bottom))
            {
                edge.GetOrigin().SetChain(Chain.Left);
            }

            foreach (var edge in new RightChainIterator(top, bottom))
            {
                edge.GetOrigin().SetChain(Chain.Right);
            }
        }

        public static Order GetOrder(Face face)
        {
            var sum = 0f;

            foreach (var edge in new HalfEdgesIterator(face))
            {
                var p1 = edge.GetOrigin().GetPosition();
                var p2 = edge.GetDestination().GetPosition();
                sum += (p2.x - p1.x) * (p2.y + p1.y);
            }

            // Handled out of due diligence but should never be encountered in practice.
            if (sum == 0)
            {
                return Order.None;
            }

            return sum > 0 ? Order.ClockWise : Order.CounterClockWise;
        }

        // Saves a sort, just merge chains. Only works for monotone polygons.
        public static void SortSweepMonotone(Vertex[] vertices, Face face)
        {
            GetTopAndBottomVertices(face, out var top, out var bottom);

            // We use a queue for the left chain as we'll receive top vertices first.
            // We use a stack for the right chain as we'll receive bottom vertices first.
            // To merge we'll start from the top.
            s_VerticesStack.Clear();
            s_VerticesQueue.Clear();

            foreach (var edge in new LeftChainIterator(top, bottom))
            {
                s_VerticesQueue.Enqueue(edge.GetOrigin());
            }

            foreach (var edge in new RightChainIterator(top, bottom))
            {
                s_VerticesStack.Push(edge.GetOrigin());
            }

            // Merge.
            var comparer = new VertexSweepComparer();
            var index = 0;
            while (s_VerticesQueue.Count != 0 && s_VerticesStack.Count != 0)
            {
                if (comparer.Compare(s_VerticesQueue.Peek(), s_VerticesStack.Peek()) < 0)
                {
                    vertices[index++] = s_VerticesQueue.Dequeue();
                }
                else
                {
                    vertices[index++] = s_VerticesStack.Pop();
                }
            }

            // Add remaining if any.
            while (s_VerticesQueue.Count != 0)
            {
                vertices[index++] = s_VerticesQueue.Dequeue();
            }

            while (s_VerticesStack.Count != 0)
            {
                vertices[index++] = s_VerticesStack.Pop();
            }

            Assert.IsTrue(index == vertices.Length);
        }

        public static bool IsMonotone(DoublyConnectedEdgeList dcel)
        {
            // By convention the inner face will be at index 0.
            return IsMonotone(new Face(dcel, 0));
        }

        public static bool IsMonotone(Face face)
        {
            // A polygon is monotone if it has no split or merge vertices.
            foreach (var edge in new HalfEdgesIterator(face))
            {
                switch (ClassifyVertex(edge.GetOrigin()))
                {
                    case VertexType.Split:
                    case VertexType.Merge:
                        return false;
                }
            }

            return true;
        }

        public static VertexType ClassifyVertex(Vertex v)
        {
            var angle = Vector2.SignedAngle(
                v.GetIncidentEdge().GetPrev().GetDirection(),
                v.GetIncidentEdge().GetDirection());

            var comparer = new VertexSweepComparer();
            var prevVertex = v.GetIncidentEdge().GetPrev().GetOrigin();
            var nextVertex = v.GetIncidentEdge().GetDestination();
            var compPrev = comparer.Compare(prevVertex, v);
            var compNext = comparer.Compare(v, nextVertex);

            // If the 2 neighbors lie above.
            if (compPrev < 0 && compNext > 0)
            {
                return angle > 0 ? VertexType.Stop : VertexType.Merge;
            }

            // If the 2 neighbors lie below.
            if (compPrev > 0 && compNext < 0)
            {
                return angle > 0 ? VertexType.Start : VertexType.Split;
            }

            return VertexType.Regular;
        }

        public static int CountHalfEdges(Face face)
        {
            var count = 0;
            foreach (var _ in new HalfEdgesIterator(face))
            {
                ++count;
            }

            return count;
        }

        // This method may seem oddly specific but it is important performance wise.
        // There's no point in walking all along the edge cycle when we are only interested
        // in knowing whether we're dealing with a triangle, a quad, or something else.
        public FaceType GetFaceType(Face face)
        {
            // The limited walk along the edge cycle allows us to unroll the loop.
            // Note that NO face ever has less than 3 edges.
            var firstEdgeIndex = m_Faces[face.Index].OuterComponent;

            // Jump along the 2 next edges.
            var edge = m_Edges[firstEdgeIndex];
            edge = m_Edges[edge.Next];
            edge = m_Edges[edge.Next];

            // If the current edge point to the first, we have 3 edges and a triangle.
            if (edge.Next == firstEdgeIndex)
            {
                return FaceType.Triangle;
            }

            // One more jump. If the current edge point to the first, we have 4 edges and a quad.
            edge = m_Edges[edge.Next];
            if (edge.Next == firstEdgeIndex)
            {
                return FaceType.Quad;
            }

            // Otherwise we have more than 4 edges.
            return FaceType.Other;
        }

        // TODO we could easily allow flipping the winding order.
        // Render a triangle geometry from the DCEL.
        public void ExtractTriangles(out NativeArray<float2> vertices, out NativeArray<int> indices, Allocator allocator)
        {
            Assert.IsTrue(m_Faces.Length > 1);
            Assert.IsTrue(m_Vertices.Length > 2);

            // Minus 1 to ignore the outer face.
            indices = new NativeArray<int>((m_Faces.Length - 1) * 3, allocator);
            vertices = new NativeArray<float2>(m_Vertices.Length, allocator);

            // Start at one to ignore the outer face.
            // We unroll the loop for performance reasons.
            // If the face isn't a triangle, we'll simply ignore vertices beyond the third.
            // It will lead to an incorrect geometry but will not raise errors.
            for (var i = 1; i != m_Faces.Length; ++i)
            {
                var edge = m_Edges[m_Faces[i].OuterComponent];
                var index = (i - 1) * 3;
                indices[index] = edge.Origin;
                edge = m_Edges[edge.Next];
                indices[index + 1] = edge.Origin;
                edge = m_Edges[edge.Next];
                indices[index + 2] = edge.Origin;
#if DEBUG

                // Check that the face actually is a triangle.
                if (m_Faces[i].OuterComponent != edge.Next)
                {
                    var numEdges = CountHalfEdges(new Face(this, i));
                    throw new InvalidOperationException(
                        $"Face at index {i} has {numEdges} edges.");
                }
#endif
            }

            for (var i = 0; i != m_Vertices.Length; ++i)
            {
                vertices[i] = m_Vertices[i].Position;
            }
        }
    }
}
