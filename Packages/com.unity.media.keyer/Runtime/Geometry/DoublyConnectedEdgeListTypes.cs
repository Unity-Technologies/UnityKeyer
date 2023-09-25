using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Unity.Media.Keyer.Geometry
{
    // Circular order.
    enum Order : short
    {
        None,
        ClockWise,
        CounterClockWise,
    }

    // Vertex chains on monotone polygons.
    enum Chain : byte
    {
        None,
        Right,
        Left
    }

    // Used for vertex incident edge re-assignament when splitting faces.
    enum EdgeAssign : byte
    {
        None,
        Origin,
        Destination
    }

    // Used for optimizations.
    enum FaceType : byte
    {
        Other,
        Triangle,
        Quad
    }

    enum VertexType : byte
    {
        Start,
        Stop,
        Split,
        Merge,
        Regular
    };

    partial class DoublyConnectedEdgeList
    {
        // VertexData, FaceData, HalfEdgeData are types used internally by the DCEL.
        // Vertex, Face, HalfEdge act as handles allowing to write readable code.
        // Their purpose is to try and make the code as readable as it would be were we using reference types.
        struct VertexData
        {
            public float2 Position;
            public Chain Chain;
            public int IncidentEdge;
        }

        struct FaceData
        {
            // We can add innerComponents when needed.
            // We do not handle holes at the moment.
            public int OuterComponent;
        }

        struct HalfEdgeData
        {
            public int Origin;
            public int IncidentFace;
            public int Twin;
            public int Prev;
            public int Next;
        }

        public readonly struct Vertex
        {
            readonly DoublyConnectedEdgeList m_Dcel;
            readonly int m_Index;

            public int Index => m_Index;

            public Vertex(DoublyConnectedEdgeList dcel, int index)
            {
                m_Dcel = dcel;
                m_Index = index;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetIsValid() => m_Dcel != null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 GetPosition() => m_Dcel.m_Vertices[m_Index].Position;

            // To lighten user code. Seems small but compounds.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetX() => GetPosition().x;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float GetY() => GetPosition().y;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetChain(Chain value)
            {
                var data = m_Dcel.m_Vertices[m_Index];
                data.Chain = value;
                m_Dcel.m_Vertices[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Chain GetChain() => m_Dcel.m_Vertices[m_Index].Chain;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetIncidentEdge(HalfEdge value)
            {
                var data = m_Dcel.m_Vertices[m_Index];
                data.IncidentEdge = value.Index;
                m_Dcel.m_Vertices[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HalfEdge GetIncidentEdge() => new(m_Dcel, m_Dcel.m_Vertices[m_Index].IncidentEdge);

            // To lighten user code.
            public static implicit operator float2(Vertex d) => d.GetPosition();

            // We deliberately only consider index in comparisons.
            public static bool operator ==(Vertex lhs, Vertex rhs) => lhs.Index == rhs.Index;

            public static bool operator !=(Vertex lhs, Vertex rhs) => !(lhs == rhs);

            // Silence warnings, but won't actually need those.
            public override bool Equals(object obj) => m_Index == ((Vertex)obj).m_Index;

            public override int GetHashCode() => m_Index;
        }

        public readonly struct Face
        {
            readonly DoublyConnectedEdgeList m_Dcel;
            readonly int m_Index;

            public int Index => m_Index;

            public Face(DoublyConnectedEdgeList dcel, int index)
            {
                m_Dcel = dcel;
                m_Index = index;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetIsValid() => m_Dcel != null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetOuterComponent(HalfEdge value)
            {
                var data = m_Dcel.m_Faces[m_Index];
                data.OuterComponent = value.Index;
                m_Dcel.m_Faces[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HalfEdge GetOuterComponent() => new(m_Dcel, m_Dcel.m_Faces[m_Index].OuterComponent);

            // We deliberately only consider index in comparisons.
            public static bool operator ==(Face lhs, Face rhs) => lhs.Index == rhs.Index;

            public static bool operator !=(Face lhs, Face rhs) => !(lhs == rhs);

            // Silence warnings, but won't actually need those.
            public override bool Equals(object obj) => m_Index == ((Face)obj).m_Index;

            public override int GetHashCode() => m_Index;
        }

        public struct HalfEdge
        {
            readonly DoublyConnectedEdgeList m_Dcel;
            readonly int m_Index;

            public int Index => m_Index;

            public HalfEdge(DoublyConnectedEdgeList dcel, int index)
            {
                m_Dcel = dcel;
                m_Index = index;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetIsValid() => m_Dcel != null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetOrigin(Vertex value)
            {
                var data = m_Dcel.m_Edges[m_Index];
                data.Origin = value.Index;
                m_Dcel.m_Edges[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vertex GetOrigin() => new(m_Dcel, m_Dcel.m_Edges[m_Index].Origin);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vertex GetDestination() => GetTwin().GetOrigin();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetIncidentFace(Face value)
            {
                var data = m_Dcel.m_Edges[m_Index];
                data.IncidentFace = value.Index;
                m_Dcel.m_Edges[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Face GetIncidentFace() => new(m_Dcel, m_Dcel.m_Edges[m_Index].IncidentFace);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetTwin(HalfEdge value)
            {
                var data = m_Dcel.m_Edges[m_Index];
                data.Twin = value.Index;
                m_Dcel.m_Edges[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HalfEdge GetTwin() => new(m_Dcel, m_Dcel.m_Edges[m_Index].Twin);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetPrev(HalfEdge value)
            {
                var data = m_Dcel.m_Edges[m_Index];
                data.Prev = value.Index;
                m_Dcel.m_Edges[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HalfEdge GetPrev() => new(m_Dcel, m_Dcel.m_Edges[m_Index].Prev);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetNext(HalfEdge value)
            {
                var data = m_Dcel.m_Edges[m_Index];
                data.Next = value.Index;
                m_Dcel.m_Edges[m_Index] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HalfEdge GetNext() => new(m_Dcel, m_Dcel.m_Edges[m_Index].Next);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public float2 GetDirection() => GetDestination().GetPosition() - GetOrigin().GetPosition();

            // To lighten user code.
            public static implicit operator float2x2(HalfEdge edge) => new float2x2
            {
                c0 = edge.GetOrigin(),
                c1 = edge.GetDestination()
            };

            // We deliberately only consider index in comparisons.
            public static bool operator ==(HalfEdge lhs, HalfEdge rhs) => lhs.Index == rhs.Index;

            public static bool operator !=(HalfEdge lhs, HalfEdge rhs) => !(lhs == rhs);

            // Silence warnings, but won't actually need those.
            public override bool Equals(object obj) => m_Index == ((HalfEdge)obj).m_Index;

            public override int GetHashCode() => m_Index;
        }
    }
}
