using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(PolygonGenerator))]
    public class TriangulatePolygonDemo : BasePolygonConsumer
    {
        static readonly Bounds k_Bounds = new(Vector3.one * .5f, Vector3.one);

        [SerializeField, Range(0.01f, 1)]
        float m_Delay;
        [SerializeField]
        SplitToMonotoneGizmos.ViewOptions m_SplitToMonotoneViewOptions;
        [SerializeField]
        TriangulateMonotoneGizmos.ViewOptions m_TriangulateMonotoneViewOptions;

        readonly DoublyConnectedEdgeList m_Dcel = new();
        readonly Stack<DoublyConnectedEdgeList.Face> m_FacesPendingTriangulation = new();
        readonly Dictionary<int, VertexType> m_VerticesClassification = new();
        readonly SplitToMonotone.SweepStatus m_SweepStatus = new();
        readonly SplitToMonotoneDoublyConnectedEdgeListMemento m_SplitToMonotoneMemento = new();
        SplitToMonotoneDoublyConnectedEdgeListMemento.Snapshot m_SplitToMonotoneSnapshot;
        readonly TriangulateMonotoneDoublyConnectedEdgeListMemento m_TriangulateMonotoneMemento = new();
        TriangulateMonotoneDoublyConnectedEdgeListMemento.Snapshot m_TriangulateMonotoneSnapshot;
        readonly TriangulateMonotone.VertexStack m_VerticesStack = new();

        Mesh m_Mesh;


        protected override void OnDisable()
        {
            StopAllCoroutines();
            m_Dcel.Dispose();
            base.OnDisable();
        }

        void OnDrawGizmos()
        {
            if (m_SplitToMonotoneMemento != null)
            {
                SplitToMonotoneGizmos.Draw(
                    m_SplitToMonotoneViewOptions,
                    m_Dcel,
                    m_SplitToMonotoneMemento,
                    m_SplitToMonotoneSnapshot,
                    m_VerticesClassification);
            }

            if (m_TriangulateMonotoneMemento != null)
            {
                TriangulateMonotoneGizmos.Draw(
                    m_TriangulateMonotoneViewOptions,
                    m_TriangulateMonotoneMemento,
                    m_TriangulateMonotoneSnapshot);
            }
        }

        protected override void Execute(BasePolygon polygon)
        {
            StopAllCoroutines();
            StartCoroutine(Run(polygon));
        }

        void UpdateMesh()
        {
            if (m_Mesh == null)
            {
                m_Mesh = new Mesh
                {
                    hideFlags = HideFlags.DontSave,
                    bounds = k_Bounds
                };
            }
            else
            {
                m_Mesh.Clear();
            }

            m_Dcel.ExtractTriangles(out var vertices2d, out var indices, Allocator.Temp);
            var vertices = new NativeArray<float3>(vertices2d.Length, Allocator.Temp);
            for (var i = 0; i != vertices2d.Length; ++i)
            {
                var pos2d = vertices2d[i];
                vertices[i] = new float3(pos2d.x, pos2d.y, 0);
            }
            m_Mesh.SetVertices(vertices);
            m_Mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
        }

        IEnumerator Run(BasePolygon polygon)
        {
            m_SplitToMonotoneSnapshot = default;

            var verticesCcw = polygon.GetVerticesCcw();
            if (verticesCcw.IsCreated)
            {
                m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Persistent);

                // FromCcwVertices puts the inner face at index 0.
                m_SplitToMonotoneMemento.Initialize(m_Dcel, m_SweepStatus);
                SplitToMonotone.Execute(m_SplitToMonotoneMemento, m_Dcel.GetInnerFace(), m_SweepStatus, m_VerticesClassification);

                // First split to monotone.
                foreach (var snapshot in m_SplitToMonotoneMemento.Snapshots)
                {
                    m_SplitToMonotoneSnapshot = snapshot;

                    yield return new WaitForSeconds(m_Delay);
                }

                m_FacesPendingTriangulation.Clear();

                // Then triangulate each face, which is a monotone polygon.
                // We must collect faces locally as the collection will change during execution.
                foreach (var face in m_Dcel.GetFacesIterator())
                {
                    if (face.Index == m_Dcel.OuterFaceIndex)
                    {
                        continue;
                    }

                    // TODO Is it worth looking for optimization beyond quads?
                    switch (m_Dcel.GetFaceType(face))
                    {
                        // No further triangulation needed.
                        case FaceType.Triangle:
                            continue;

                        // Quad is trivially reduced to triangles,
                        // no need to run a full monotone triangulation.
                        case FaceType.Quad:
                        {
                            var edgeA = face.GetOuterComponent();
                            var edgeB = edgeA.GetNext().GetNext();
                            m_Dcel.SplitFace(edgeA, edgeB, EdgeAssign.None, out _);
                        }
                            continue;

                        // Needs monotone triangulation.
                        case FaceType.Other:
                            m_FacesPendingTriangulation.Push(face);
                            break;
                    }
                }

                while (m_FacesPendingTriangulation.Count > 0)
                {
                    var face = m_FacesPendingTriangulation.Pop();

                    // By this point, we are iterating through the faces of the original DCEL.
                    Assert.IsTrue(DoublyConnectedEdgeList.GetOrder(face) == Order.CounterClockWise);

                    // We must ensure that all the vertices we are about to process have an incident edge on the current face.
                    DoublyConnectedEdgeList.EnsureVerticesIncidentEdgesAreOnFace(face);

                    m_TriangulateMonotoneSnapshot = default;
                    m_TriangulateMonotoneMemento.Initialize(m_Dcel, m_VerticesStack);

                    TriangulateMonotone.Execute(m_TriangulateMonotoneMemento, face, m_VerticesStack);

                    // Playback for visualization.
                    foreach (var snapshot in m_TriangulateMonotoneMemento.Snapshots)
                    {
                        m_TriangulateMonotoneSnapshot = snapshot;

                        yield return new WaitForSeconds(m_Delay);
                    }
                }

                UpdateMesh();
            }
            else
            {
                Debug.LogError("Failed to read polygon vertices.");
            }
        }
    }
}
