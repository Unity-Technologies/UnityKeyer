using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Unity.Media.Keyer.Geometry.Demo
{
    static class SplitToMonotoneGizmos
    {
        [Flags]
        public enum ViewOptions : short
        {
            None = 0,
            Polygon = 1 << 0,
            Sweep = 1 << 1,
            AddedEdges = 1 << 2,
            VertexClassification = 1 << 3,
        };

        public static void Draw(
            ViewOptions viewOptions,
            DoublyConnectedEdgeList dcel,
            SplitToMonotoneDoublyConnectedEdgeListMemento memento,
            SplitToMonotoneDoublyConnectedEdgeListMemento.Snapshot snapshot,
            Dictionary<int, VertexType> verticesClassification)
        {
            if (viewOptions.HasFlag(ViewOptions.Polygon))
            {
                Random.InitState(0);
                foreach (var face in dcel.GetFacesIterator())
                {
                    Gizmos.color = Random.ColorHSV();
                    foreach (var edge in new DoublyConnectedEdgeList.HalfEdgesIterator(face))
                    {
                        Gizmos.DrawLine(edge.GetOrigin().GetPosition().AsVec3(), edge.GetDestination().GetPosition().AsVec3());
                    }
                }
            }

            if (viewOptions.HasFlag(ViewOptions.VertexClassification))
            {
                Gizmos.color = Color.cyan;

                foreach (var vertex in dcel.GetVerticesIterator())
                {
                    switch (verticesClassification[vertex.Index])
                    {
                        case VertexType.Start:
                            Gizmos.DrawWireCube(vertex.GetPosition().AsVec3(), Vector3.one * 0.01f);
                            break;
                        case VertexType.Stop:
                            Gizmos.DrawCube(vertex.GetPosition().AsVec3(), Vector3.one * 0.01f);
                            break;
                        case VertexType.Regular:
                            Gizmos.DrawSphere(vertex.GetPosition().AsVec3(), 0.01f);
                            break;
                        case VertexType.Split:
                        {
                            var a = vertex.GetPosition().AsVec3() + Vector3.left * 0.01f;
                            var b = vertex.GetPosition().AsVec3() + Vector3.right * 0.01f;
                            var c = vertex.GetPosition().AsVec3() + Vector3.up * 0.02f;
                            Gizmos.DrawLine(a, b);
                            Gizmos.DrawLine(b, c);
                            Gizmos.DrawLine(c, a);
                        }
                        break;
                        case VertexType.Merge:
                        {
                            var a = vertex.GetPosition().AsVec3() + Vector3.left * 0.01f;
                            var b = vertex.GetPosition().AsVec3() + Vector3.right * 0.01f;
                            var c = vertex.GetPosition().AsVec3() + Vector3.down * 0.02f;
                            Gizmos.DrawLine(a, b);
                            Gizmos.DrawLine(b, c);
                            Gizmos.DrawLine(c, a);
                        }
                        break;
                    }
                }
            }

            if (viewOptions.HasFlag(ViewOptions.AddedEdges))
            {
                Gizmos.color = Color.green;

                var index = 0;
                foreach (var edge in memento.AddedEdges)
                {
                    if (index == snapshot.EdgeCount)
                    {
                        break;
                    }

                    Gizmos.DrawLine(edge.c0.AsVec3(), edge.c1.AsVec3());
                    ++index;
                }
            }

            if (viewOptions.HasFlag(ViewOptions.Sweep))
            {
                // We want to observe edge orientation as well, hence the sphere at the origin.
                for (var i = 0; i != snapshot.EdgeAndHelperCount; ++i)
                {
                    var edgeAndHelper = memento.EdgesAndHelpers.ElementAt(snapshot.EdgeAndHelperIndex + i);

                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(edgeAndHelper.Edge.c0.AsVec3(), edgeAndHelper.Edge.c1.AsVec3());
                    Gizmos.DrawSphere(edgeAndHelper.Edge.c0.AsVec3(), 0.01f);
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(edgeAndHelper.Helper.c0.AsVec3(), edgeAndHelper.Helper.c1.AsVec3());
                    Gizmos.DrawSphere(edgeAndHelper.Helper.c0.AsVec3(), 0.01f);
                }

                Gizmos.DrawLine(new Vector2(-100, snapshot.SweepLineY), new Vector2(100, snapshot.SweepLineY));
            }
        }
    }
}
