using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Media.Keyer.Geometry
{
    partial class DoublyConnectedEdgeList : IDoublyConnectedEdgeList
    {
        // Convention.
        const int k_OuterFaceIndex = 0;
        const int k_InnerFaceIndex = 1;

        // Array for vertices as we do not add vertices.
        NativeArray<VertexData> m_Vertices;
        UnsafeList<HalfEdgeData> m_Edges;
        UnsafeList<FaceData> m_Faces;
        bool m_NativeCollectionsRequireDisposal;

        public void Dispose()
        {
            // IsCreated only checks that the pointer is not null.
            // It is possible for a Temp alloc memory to have been recycled already,
            // making the call to Dispose() throw an error.
            if (m_NativeCollectionsRequireDisposal)
            {
                if (m_Vertices.IsCreated)
                {
                    m_Vertices.Dispose();
                }

                if (m_Edges.IsCreated)
                {
                    m_Edges.Dispose();
                }

                if (m_Faces.IsCreated)
                {
                    m_Faces.Dispose();
                }
            }
        }

        void Initialize(
            NativeArray<VertexData> vertices,
            UnsafeList<HalfEdgeData> edges,
            UnsafeList<FaceData> faces,
            bool nativeCollectionsRequireDisposal)
        {
            m_Vertices = vertices;
            m_Edges = edges;
            m_Faces = faces;

            m_NativeCollectionsRequireDisposal = nativeCollectionsRequireDisposal;
        }

        public VerticesIterator GetVerticesIterator() => new(this);

        public FacesIterator GetFacesIterator() => new(this);

        public Face GetInnerFace()
        {
            if (!m_Faces.IsCreated || m_Faces.Length < k_InnerFaceIndex + 1)
            {
                throw new InvalidOperationException("Cannot access inner face.");
            }

            return new Face(this, k_InnerFaceIndex);
        }

        public int OuterFaceIndex => k_OuterFaceIndex;

        Face CreateFace(FaceData face)
        {
            m_Faces.Add(face);
            return new Face(this, m_Faces.Length - 1);
        }

        HalfEdge CreateEdge(HalfEdgeData edge)
        {
            m_Edges.Add(edge);
            return new HalfEdge(this, m_Edges.Length - 1);
        }

        // We deliberately populate with invalid values,
        // we must not allow confusion when it comes to unassigned fields.
        Face CreateFace() => CreateFace(new FaceData
        {
            OuterComponent = -1
        });

        HalfEdge CreateEdge() => CreateEdge(new HalfEdgeData
        {
            Origin = -1,
            IncidentFace = -1,
            Twin = -1,
            Prev = -1,
            Next = -1
        });

        public void SplitFace(HalfEdge edge, Vertex vertex)
        {
            SplitFace(edge, vertex, EdgeAssign.None, out _);
        }

        // Parameter vertex is only used in debug implementations of IDoublyConnectedEdgeList.
        public void SplitFace(HalfEdge left, HalfEdge right, EdgeAssign edgeAssign, Vertex vertex)
        {
            SplitFace(left, right, edgeAssign, out _);
        }

        // A more permissive SplitFace that will walk around edges to find a connectable half-edge pair.
        // In normal mode we'll only check for faces.
        // We should not write code that relies on additional tests.
        public void SplitFace(HalfEdge edge, Vertex vertex, EdgeAssign edgeAssign, out HalfEdge newEdge)
        {
            var error = String.Empty;
            foreach (var vertexEdge in new HalfEdgesConnectedToVertexIterator(vertex))
            {
                if (CanSplitFace(edge, vertexEdge, out _, out error))
                {
                    SplitFaceInternal(edge, vertexEdge, edgeAssign, out newEdge);
                    return;
                }
            }

            throw new InvalidOperationException(error);
        }

        void SplitFaceInternal(HalfEdge edgeA, HalfEdge edgeB, EdgeAssign edgeAssign, out HalfEdge newEdge)
        {
            var face = edgeA.GetIncidentFace();

            // Create two new edges and a new face;
            newEdge = CreateEdge();
            newEdge.SetOrigin(edgeA.GetOrigin());
            newEdge.SetIncidentFace(face);
            face.SetOuterComponent(newEdge);
            var newEdgeTwin = CreateEdge();
            newEdgeTwin.SetOrigin(edgeB.GetOrigin());
            var newFace = CreateFace();
            newFace.SetOuterComponent(newEdgeTwin);

            // Connect twins.
            newEdge.SetTwin(newEdgeTwin);
            newEdgeTwin.SetTwin(newEdge);

            // Connect edges.
            newEdge.SetPrev(edgeA.GetPrev());
            newEdge.SetNext(edgeB);
            newEdgeTwin.SetPrev(edgeB.GetPrev());
            newEdgeTwin.SetNext(edgeA);

            edgeA.GetPrev().SetNext(newEdge);
            edgeB.GetPrev().SetNext(newEdgeTwin);
            edgeB.SetPrev(newEdge);
            edgeA.SetPrev(newEdgeTwin);

            switch (edgeAssign)
            {
                case EdgeAssign.None:
                    break;
                case EdgeAssign.Origin:
                    newEdge.GetOrigin().SetIncidentEdge(newEdge);
                    break;
                case EdgeAssign.Destination:
                    newEdgeTwin.GetOrigin().SetIncidentEdge(newEdgeTwin);
                    break;
            }

            // Assign new face.
            foreach (var edge in new HalfEdgesIterator(newEdgeTwin))
            {
                edge.SetIncidentFace(newFace);
            }
        }

        public void SplitFace(HalfEdge edgeA, HalfEdge edgeB, EdgeAssign edgeAssign, out HalfEdge newEdge)
        {
#if DEBUG
            if (!CanSplitFace(edgeA, edgeB, out var halfEdgesOnFace, out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }
#endif
            SplitFaceInternal(edgeA, edgeB, edgeAssign, out newEdge);
#if DEBUG

            // Verify that the number of half-edges before and after split are consistent.
            var nEdges1 = CountHalfEdges(newEdge.GetIncidentFace());
            var nEdges2 = CountHalfEdges(newEdge.GetTwin().GetIncidentFace());
            var edgesCountsMatch = halfEdgesOnFace + 2 == nEdges1 + nEdges2;
            if (!edgesCountsMatch)
            {
                throw new InvalidOperationException(
                    $"Edge counts don't match, from {halfEdgesOnFace} to {nEdges1} and {nEdges2}.");
            }
#endif
        }

        static bool CanSplitFace(HalfEdge edgeA, HalfEdge edgeB, out int halfEdgesOnFace, out string errorMessage)
        {
            if (edgeA == edgeB)
            {
                errorMessage = "Edges are equal.";
                halfEdgesOnFace = 0;
                return false;
            }

            // We always test for faces, and have additional test in debug mode.
            if (edgeA.GetIncidentFace() != edgeB.GetIncidentFace())
            {
                errorMessage = "Edges are not on the same face.";
                halfEdgesOnFace = 0;
                return false;
            }

#if DEBUG
            if (edgeA.GetIncidentFace().Index == k_OuterFaceIndex)
            {
                errorMessage = "Cannot split outer face.";
                halfEdgesOnFace = 0;
                return false;
            }

            if (edgeA.GetOrigin() == edgeB.GetDestination() || edgeA.GetDestination() == edgeB.GetOrigin())
            {
                errorMessage = "Edges are already connected.";
                halfEdgesOnFace = 0;
                return false;
            }

            halfEdgesOnFace = CountHalfEdges(edgeA.GetIncidentFace());
            if (halfEdgesOnFace < 4)
            {
                errorMessage = "Can't split a face with less than 4 edges.";
                return false;
            }

            // That test should not be needed, redundant.
            var e = edgeA;
            do
            {
                e = e.GetNext();

                if (e == edgeB)
                {
                    errorMessage = String.Empty;
                    return true;
                }
            }
            while (e != edgeA);

            errorMessage = "Edges are not on the same cycle.";
            return false;
#else
            // halfEdgesOnFace is only used for checks in DEBUG mode.
            halfEdgesOnFace = 0;
            errorMessage = String.Empty;
            return true;
#endif
        }

        public void InitializeFromCcwVertices(NativeArray<float2> verticesCcw, Allocator allocator)
        {
            Dispose();
#if DEBUG
            Assert.IsTrue(Utilities.GetOrder(verticesCcw) == Order.CounterClockWise);
#endif
            // We must track the allocator type to determine whether we should manually dispose the native collections.
            var persistent = false;
            switch (allocator)
            {
                case Allocator.Persistent:
                    persistent = true;
                    break;
                case Allocator.Temp:
                case Allocator.TempJob:
                    break;
                default:
                    throw new ArgumentException($"Unsupported allocator {allocator}.");
            }

            var len = verticesCcw.Length;
            var vertices = new NativeArray<VertexData>(len, allocator);
            var edges = new UnsafeList<HalfEdgeData>(len * 2, allocator);
            var faces = new UnsafeList<FaceData>(2, allocator);

            // So we can index instead of Add().
            edges.Length = len * 2;
            faces.Length = 2;

            // Outer face.
            faces[0] = new FaceData
            {
                OuterComponent = len
            };

            // Inner face.
            faces[1] = new FaceData
            {
                OuterComponent = 0
            };

            for (var i = 0; i != len; ++i)
            {
                vertices[i] = new VertexData
                {
                    Position = verticesCcw[i],
                    Chain = Chain.None,
                    IncidentEdge = i,
                };

                edges[i] = new HalfEdgeData
                {
                    Origin = i,
                    IncidentFace = 1,
                    Twin = i + len,
                    Prev = (i - 1 + len) % len,
                    Next = (i + 1) % len
                };

                edges[i + len] = new HalfEdgeData
                {
                    Origin = (i + 1) % len, // Twin(i) = i + len
                    IncidentFace = 0, // Outer face, by convention.
                    Twin = i,
                    Prev = (i - 1 + len) % len + len,
                    Next = (i + 1) % len + len
                };
            }

            Initialize(vertices, edges, faces, persistent);
        }
    }
}
