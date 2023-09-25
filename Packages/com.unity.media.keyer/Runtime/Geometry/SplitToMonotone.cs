using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry
{
    static class SplitToMonotone
    {
        public class SweepStatus
        {
            readonly Dictionary<DoublyConnectedEdgeList.HalfEdge, DoublyConnectedEdgeList.Vertex> m_EdgeToHelperMap = new();
            readonly Dictionary<DoublyConnectedEdgeList.HalfEdge, DoublyConnectedEdgeList.HalfEdge> m_EdgeToPrevMap = new();
            readonly List<DoublyConnectedEdgeList.HalfEdge> m_HalfEdges = new();
            float m_SweepLineY;

            public void Clear()
            {
                m_EdgeToHelperMap.Clear();
                m_EdgeToPrevMap.Clear();
                m_HalfEdges.Clear();
            }

            public float SweepLineY
            {
                set => m_SweepLineY = value;
                get => m_SweepLineY;
            }

            public DoublyConnectedEdgeList.Vertex Helper(DoublyConnectedEdgeList.HalfEdge edge)
            {
                return m_EdgeToHelperMap.TryGetValue(edge, out var helper) ? helper : default;
            }

            public DoublyConnectedEdgeList.HalfEdge FindLeft(DoublyConnectedEdgeList.Vertex vertex)
            {
                // Reverse order as rightmost edges will be at the end of the list.
                for (var i = m_HalfEdges.Count - 1; i != -1; --i)
                {
                    var edge = m_HalfEdges[i];
                    if (edge.GetDestination() == vertex)
                    {
                        continue;
                    }

                    var intersect = HalfEdgeSweepComparer.SweepIntersection(edge, m_SweepLineY, out var intersectionFound);
                    if (intersectionFound && vertex.GetX() > intersect.x)
                    {
                        return edge;
                    }
                }

                throw new InvalidOperationException("Could not find left edge.");
            }

            public void Insert(DoublyConnectedEdgeList.HalfEdge edge, DoublyConnectedEdgeList.Vertex helper)
            {
                m_EdgeToHelperMap.Add(edge, helper);
                m_HalfEdges.Add(edge);
                m_HalfEdges.Sort(new HalfEdgeSweepComparer(m_SweepLineY));
            }

            public void Delete(DoublyConnectedEdgeList.HalfEdge edge)
            {
                m_EdgeToHelperMap.Remove(edge);
                m_HalfEdges.Remove(edge);
            }

            public void UpdateHelper(DoublyConnectedEdgeList.HalfEdge edge, DoublyConnectedEdgeList.Vertex helper)
            {
                if (m_EdgeToHelperMap.ContainsKey(edge))
                {
                    m_EdgeToHelperMap[edge] = helper;
                    return;
                }

                throw new InvalidOperationException("Could not find helper edge for update.");
            }

            public IReadOnlyCollection<KeyValuePair<DoublyConnectedEdgeList.HalfEdge, DoublyConnectedEdgeList.Vertex>> EdgesAndHelpers() => m_EdgeToHelperMap;
        }

        static readonly SweepStatus s_SweepStatus = new();
        static readonly Dictionary<int, VertexType> s_VerticesClassification = new();

        public static void Execute(IDoublyConnectedEdgeList dcel, DoublyConnectedEdgeList.Face face)
        {
            Execute(dcel, face, s_SweepStatus, s_VerticesClassification);
        }

        public static void Execute(IDoublyConnectedEdgeList dcel, DoublyConnectedEdgeList.Face face, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification)
        {
            sweepStatus.Clear();

            verticesClassification.Clear();
            foreach (var vertex in dcel.GetVerticesIterator())
            {
                verticesClassification[vertex.Index] = DoublyConnectedEdgeList.ClassifyVertex(vertex);
            }

            var vertices = new DoublyConnectedEdgeList.Vertex[DoublyConnectedEdgeList.CountHalfEdges(face)];
            var index = 0;
            foreach (var edge in new DoublyConnectedEdgeList.HalfEdgesIterator(face))
            {
                vertices[index++] = edge.GetOrigin();
            }

            Array.Sort(vertices, new VertexSweepComparer());

            foreach (var vertex in vertices)
            {
                // update comparer with sweep line position
                sweepStatus.SweepLineY = vertex.GetY();

                switch (verticesClassification[vertex.Index])
                {
                    case VertexType.Start:
                        HandleStartVertex(sweepStatus, vertex);
                        break;

                    case VertexType.Stop:
                        HandleStopVertex(dcel, sweepStatus, verticesClassification, vertex);
                        break;

                    case VertexType.Split:
                        HandleSplitVertex(dcel, sweepStatus, vertex);
                        break;

                    case VertexType.Merge:
                        HandleMergeVertex(dcel, sweepStatus, verticesClassification, vertex);
                        break;

                    case VertexType.Regular:
                        HandleRegularVertex(dcel, sweepStatus, verticesClassification, vertex);
                        break;
                }
            }
        }

        static void DiagonalToPreviousEdgeHelper(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification, DoublyConnectedEdgeList.Vertex vertex)
        {
            var helperPrev = sweepStatus.Helper(vertex.GetIncidentEdge().GetPrev());
            if (verticesClassification[helperPrev.Index] == VertexType.Merge)
            {
                dcel.SplitFace(vertex.GetIncidentEdge(), helperPrev);
            }

            sweepStatus.Delete(vertex.GetIncidentEdge().GetPrev());
        }

        static void DiagonalToLeftEdgeHelper(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification, DoublyConnectedEdgeList.Vertex vertex)
        {
            var leftEdge = sweepStatus.FindLeft(vertex);
            if (verticesClassification[sweepStatus.Helper(leftEdge).Index] == VertexType.Merge)
            {
                var leftHelper = sweepStatus.Helper(leftEdge);
                dcel.SplitFace(vertex.GetIncidentEdge(), leftHelper);
            }

            sweepStatus.UpdateHelper(leftEdge, vertex);
        }

        static void HandleStartVertex(SweepStatus sweepStatus, DoublyConnectedEdgeList.Vertex vertex)
        {
            sweepStatus.Insert(vertex.GetIncidentEdge(), vertex);
        }

        static void HandleStopVertex(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification, DoublyConnectedEdgeList.Vertex vertex)
        {
            DiagonalToPreviousEdgeHelper(dcel, sweepStatus, verticesClassification, vertex);
        }

        static void HandleSplitVertex(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, DoublyConnectedEdgeList.Vertex vertex)
        {
            var leftEdge = sweepStatus.FindLeft(vertex);
            var leftHelper = sweepStatus.Helper(leftEdge);
            dcel.SplitFace(vertex.GetIncidentEdge(), leftHelper);
            sweepStatus.UpdateHelper(leftEdge, vertex);
            sweepStatus.Insert(vertex.GetIncidentEdge(), vertex);
        }

        static void HandleMergeVertex(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification, DoublyConnectedEdgeList.Vertex vertex)
        {
            DiagonalToPreviousEdgeHelper(dcel, sweepStatus, verticesClassification, vertex);
            DiagonalToLeftEdgeHelper(dcel, sweepStatus, verticesClassification, vertex);
        }

        static void HandleRegularVertex(IDoublyConnectedEdgeList dcel, SweepStatus sweepStatus, Dictionary<int, VertexType> verticesClassification, DoublyConnectedEdgeList.Vertex vertex)
        {
            // if the interior of the polygon lies to the right of vertex
            if (Vector2.SignedAngle(Vector2.right, vertex.GetIncidentEdge().GetDirection()) <= 0)
            {
                DiagonalToPreviousEdgeHelper(dcel, sweepStatus, verticesClassification, vertex);
                sweepStatus.Insert(vertex.GetIncidentEdge(), vertex);
            }
            else
            {
                DiagonalToLeftEdgeHelper(dcel, sweepStatus, verticesClassification, vertex);
            }
        }
    }
}
