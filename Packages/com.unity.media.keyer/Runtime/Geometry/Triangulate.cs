using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.Media.Keyer.Geometry
{
    static class Triangulate
    {
        static readonly Stack<DoublyConnectedEdgeList.Face> s_FacesPendingTriangulation = new();

        public static void Execute(DoublyConnectedEdgeList dcel)
        {
            // Split to monotone if needed.
            // We anticipate that a lot of polygons will be monotone already.
            if (!DoublyConnectedEdgeList.IsMonotone(dcel))
            {
                SplitToMonotone.Execute(dcel, dcel.GetInnerFace());
            }

            s_FacesPendingTriangulation.Clear();

            // Triangulate inner faces, when needed.
            foreach (var face in dcel.GetFacesIterator())
            {
                if (face.Index == dcel.OuterFaceIndex)
                {
                    continue;
                }

                switch (dcel.GetFaceType(face))
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
                        dcel.SplitFace(edgeA, edgeB, EdgeAssign.None, out _);
                    }
                    continue;

                    // Needs monotone triangulation.
                    case FaceType.Other:
                        s_FacesPendingTriangulation.Push(face);
                        break;
                }
            }

            while (s_FacesPendingTriangulation.Count > 0)
            {
                var face = s_FacesPendingTriangulation.Pop();
#if DEBUG

                // By this point, we are iterating through the faces of the original DCEL.
                Assert.IsTrue(DoublyConnectedEdgeList.GetOrder(face) == Order.CounterClockWise);
#endif

                // We must ensure that all the vertices we are about to process have an incident edge on the current face.
                DoublyConnectedEdgeList.EnsureVerticesIncidentEdgesAreOnFace(face);

                TriangulateMonotone.Execute(dcel, face);
            }
        }
    }
}
