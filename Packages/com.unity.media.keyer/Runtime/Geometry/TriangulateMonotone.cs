using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry
{
    static class TriangulateMonotone
    {
        // Forces consistency across stacks.
        public class VertexStack
        {
            readonly Stack<DoublyConnectedEdgeList.Vertex> m_Vertices = new();
            readonly Stack<int> m_Indices = new();

            public IReadOnlyCollection<DoublyConnectedEdgeList.Vertex> Vertices => m_Vertices;

            public int Count => m_Vertices.Count;

            public void Clear()
            {
                m_Vertices.Clear();
                m_Indices.Clear();
            }

            public void Pop(out DoublyConnectedEdgeList.Vertex vertex, out int index)
            {
                vertex = m_Vertices.Pop();
                index = m_Indices.Pop();
            }

            public DoublyConnectedEdgeList.Vertex Pop()
            {
                m_Indices.Pop();
                return m_Vertices.Pop();
            }

            public DoublyConnectedEdgeList.Vertex Peek() => m_Vertices.Peek();

            public void Push(DoublyConnectedEdgeList.Vertex vertex, int index)
            {
                m_Vertices.Push(vertex);
                m_Indices.Push(index);
            }
        }

        static readonly Stack<DoublyConnectedEdgeList.Vertex> s_PendingDiagonalVertices = new();
        static readonly VertexStack s_VertexStack = new();

        public static void Execute(IDoublyConnectedEdgeList dcel, DoublyConnectedEdgeList.Face face)
        {
            Execute(dcel, face, s_VertexStack);
        }

        public static void Execute(IDoublyConnectedEdgeList dcel, DoublyConnectedEdgeList.Face face, VertexStack vertexStack)
        {
            // There's an equal number of vertices and half-edges on a face.
            DoublyConnectedEdgeList.LabelChains(face);

            // TODO Use native collection.
            var vertices = new DoublyConnectedEdgeList.Vertex[DoublyConnectedEdgeList.CountHalfEdges(face)];
            DoublyConnectedEdgeList.SortSweepMonotone(vertices, face);
            Execute(dcel, vertices, vertexStack);
        }

        static void Execute(IDoublyConnectedEdgeList dcel, DoublyConnectedEdgeList.Vertex[] vertices, VertexStack vertexStack)
        {
            vertexStack.Clear();
            s_PendingDiagonalVertices.Clear();

            // the stack holds vertices we still (possibly) have edges to connect to
            vertexStack.Push(vertices[0], 0);
            vertexStack.Push(vertices[1], 1);

            for (var i = 2; i != vertices.Length - 1; ++i)
            {
                // If current vertex and the vertex on top of stack are on different chains.
                if (vertices[i].GetChain() != vertexStack.Peek().GetChain())
                {
                    // For all vertices on stack except the last one,
                    // for it is connected to the current vertex by an edge.
                    while (vertexStack.Count > 1)
                    {
                        s_PendingDiagonalVertices.Push(vertexStack.Pop());
                    }

                    // We care about the order we want diagonals to be added top to bottom,
                    // it allows us to reassign half-edges to vertices properly when splitting.
                    while (s_PendingDiagonalVertices.Count > 0)
                    {
                        var vertex = s_PendingDiagonalVertices.Pop();
                        var edgeAssign = GetEdgeAssign(vertex, vertices[i]);
                        dcel.SplitFace(vertex.GetIncidentEdge(), vertices[i].GetIncidentEdge(), edgeAssign, vertices[i]);
                    }

                    vertexStack.Clear();

                    // Push current vertex and its predecessor on the stack.
                    vertexStack.Push(vertices[i - 1], i - 1);
                    vertexStack.Push(vertices[i], i);
                }
                else
                {
                    // Pop one vertex from the stack, as it shares an edge with the current vertex.
                    vertexStack.Pop(out var lastPopped, out var lastPoppedIndex);

                    // Pop the other vertices while the diagonal from them to the current vertex is inside the polygon.
                    // Is the vertex at the top of the stack visible from the current vertex?
                    // We can deduce that knowing the previously popped vertex.
                    while (vertexStack.Count > 0 && IsInside(vertices[i], vertices[i].GetChain(), vertexStack.Peek(), lastPopped))
                    {
                        var edgeAssign = GetEdgeAssign(vertices[i], vertexStack.Peek());
                        dcel.SplitFace(vertices[i].GetIncidentEdge(), vertexStack.Peek().GetIncidentEdge(), edgeAssign, vertices[i]);
                        vertexStack.Pop(out lastPopped, out lastPoppedIndex);
                    }

                    // Push the last vertex that has been popped back onto the stack.
                    vertexStack.Push(lastPopped, lastPoppedIndex);

                    // Push the current vertex on the stack.
                    vertexStack.Push(vertices[i], i);
                }
            }

            // Add diagonals from the last vertex to all vertices on the stack except the first and the last one.
            vertexStack.Pop();
            s_PendingDiagonalVertices.Clear();

            while (vertexStack.Count > 1)
            {
                s_PendingDiagonalVertices.Push(vertexStack.Pop());
            }

            // We care about the order we want diagonals to be added top to bottom,
            // it allows us to reassign half-edges to vertices properly when splitting.
            while (s_PendingDiagonalVertices.Count > 0)
            {
                var vertex = s_PendingDiagonalVertices.Pop();
                var edgeAssign = Vector2.SignedAngle(Vector2.right, vertex.GetIncidentEdge().GetDirection()) > 0 ? EdgeAssign.Origin : EdgeAssign.Destination;
                dcel.SplitFace(vertex.GetIncidentEdge(), vertices[^1].GetIncidentEdge(), edgeAssign, vertices[^1]);
            }
        }

        static bool IsInside(DoublyConnectedEdgeList.Vertex vertex, Chain vertexChain, DoublyConnectedEdgeList.Vertex popped, DoublyConnectedEdgeList.Vertex prevPopped)
        {
            var currentEdge = popped.GetPosition() - vertex.GetPosition();
            var prevEdge = prevPopped.GetPosition() - vertex.GetPosition();
            var alpha = Vector2.SignedAngle(prevEdge, currentEdge);

            if (vertexChain == Chain.Left)
            {
                return alpha <= 0;
            }

            return alpha >= 0;
        }

        static EdgeAssign GetEdgeAssign(DoublyConnectedEdgeList.Vertex origin, DoublyConnectedEdgeList.Vertex destination)
        {
            if (origin.GetChain() == destination.GetChain())
            {
                // We reassign so that the edge whose normal points inside is used
                if (origin.GetChain() == Chain.Left)
                {
                    // Rebind the edge that points down.
                    return origin.GetY() > destination.GetY() ? EdgeAssign.Origin : EdgeAssign.Destination;
                }

                return origin.GetY() < destination.GetY() ? EdgeAssign.Origin : EdgeAssign.Destination;
            }

            // Otherwise, we reassign so that the deg whose normal points downwards is used.
            return origin.GetChain() == Chain.Left ? EdgeAssign.Destination : EdgeAssign.Origin;
        }
    }
}
