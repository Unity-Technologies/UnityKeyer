using System;
using System.Linq;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    class TriangulateMonotoneGizmos
    {
        [Flags]
        public enum ViewOptions : short
        {
            None = 0,
            Edges = 1 << 0,
            Stack = 1 << 1,
            CurrentVertex = 1 << 2,
            CurrentEdges = 1 << 3
        };

        public static void Draw(
            ViewOptions viewOptions,
            TriangulateMonotoneDoublyConnectedEdgeListMemento memento,
            TriangulateMonotoneDoublyConnectedEdgeListMemento.Snapshot snapshot)
        {
            // Draw the edges we already added during the execution.
            if (viewOptions.HasFlag(ViewOptions.Edges))
            {
                Gizmos.color = Color.gray;

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

            // Draw the current vertex stack.
            if (viewOptions.HasFlag(ViewOptions.Stack))
            {
                for (var i = 0; i != snapshot.StackCount; ++i)
                {
                    var vertex = memento.StackSnapShots.ElementAt(snapshot.StackIndex + i);

                    Gizmos.color = Color.blue;
                    Gizmos.DrawSphere(vertex.AsVec3(), 0.02f);

                    // Draw dots to represent the vertex index in the stack.
                    // Wish we could just draw text...
                    Gizmos.color = Color.white;
                    for (var j = 0; j != i + 1; ++j)
                    {
                        var from = vertex.AsVec3() + Vector3.left * .04f + Vector3.left * (j * .03f);
                        Gizmos.DrawSphere(from, 0.01f);
                    }
                }
            }

            // Draw the current vertex, the one we are using to add diagonals.
            if (viewOptions.HasFlag(ViewOptions.CurrentVertex) && snapshot.Vertex.IsDefault())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(snapshot.Vertex.AsVec3(), 0.02f);
            }

            // Draw the edges we are currently considering for adding a diagonal.
            // They are expected to lie on the original inner face.
            if (viewOptions.HasFlag(ViewOptions.CurrentEdges) && !snapshot.PendingEdgeA.IsDefault() && !snapshot.PendingEdgeB.IsDefault())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(snapshot.PendingEdgeA.c0.AsVec3(), snapshot.PendingEdgeA.c1.AsVec3());
                Gizmos.DrawSphere(snapshot.PendingEdgeA.c0.AsVec3(), 0.01f);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(snapshot.PendingEdgeB.c0.AsVec3(), snapshot.PendingEdgeB.c1.AsVec3());
                Gizmos.DrawSphere(snapshot.PendingEdgeB.c0.AsVec3(), 0.01f);
            }
        }
    }
}
