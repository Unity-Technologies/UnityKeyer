using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry
{
    readonly struct VertexSweepComparer : IComparer<DoublyConnectedEdgeList.Vertex>
    {
        public int Compare(DoublyConnectedEdgeList.Vertex lhs, DoublyConnectedEdgeList.Vertex rhs)
        {
            // Strict comparison is deliberate.
            if (lhs.GetY() == rhs.GetY())
            {
                return lhs.GetX().CompareTo(rhs.GetX());
            }

            // On purpose, we go in descending Y order.
            return rhs.GetY().CompareTo(lhs.GetY());
        }
    }

    // sort status edges based on their intersection with the sweep line
    readonly struct HalfEdgeSweepComparer : IComparer<DoublyConnectedEdgeList.HalfEdge>
    {
        readonly float m_SweepLineY;

        public HalfEdgeSweepComparer(float sweepLineY)
        {
            m_SweepLineY = sweepLineY;
        }

        public int Compare(DoublyConnectedEdgeList.HalfEdge lhs, DoublyConnectedEdgeList.HalfEdge rhs)
        {
            var leftX = SweepIntersection(lhs, m_SweepLineY, out _).x;
            var rightX = SweepIntersection(rhs, m_SweepLineY, out _).x;
            return leftX.CompareTo(rightX);
        }

        public static Vector2 SweepIntersection(DoublyConnectedEdgeList.HalfEdge edge, float sweepY, out bool found)
        {
            return SweepIntersection(edge.GetOrigin().GetPosition(), edge.GetDestination().GetPosition(), sweepY, out found);
        }

        static Vector2 SweepIntersection(Vector2 origin, Vector2 destination, float sweepY, out bool found)
        {
            var r = (origin.y - sweepY) / (origin.y - destination.y);

            // add a bit of tolerance as some vertices may have the same Y coordinate
            if (r > 1 || r < 0)
            {
                found = false;
                return Vector2.zero;
            }

            found = true;
            return Vector2.Lerp(origin, destination, r);
        }
    }
}
