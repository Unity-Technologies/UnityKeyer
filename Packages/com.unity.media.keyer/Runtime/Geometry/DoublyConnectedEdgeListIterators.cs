using System;

namespace Unity.Media.Keyer.Geometry
{
    partial class DoublyConnectedEdgeList
    {
        // The foreach statement is duck-typed,
        // meaning that the compiler first looks for public methods with the right signatures
        // (GetEnumerator(), MoveNext() and Current) regardless of whether they are implementations of these interfaces,
        // and only falls back to the interfaces if necessary.
        // We would degrade performance by using interfaces.
        public struct VertexEnumerator
        {
            readonly Vertex m_Start;
            readonly Vertex m_End;
            Vertex m_Current;

            public VertexEnumerator(Vertex start, Vertex end)
            {
                m_Start = start;
                m_End = end;
                m_Current = default;
            }

            public Vertex Current => m_Current;

            public bool MoveNext()
            {
                if (!m_Current.GetIsValid())
                {
                    m_Current = m_Start;
                    return true;
                }

                m_Current = m_Current.GetIncidentEdge().GetDestination();
                return m_Current != m_End;
            }
        }

        public struct HalfEdgeEnumerator
        {
            readonly HalfEdge m_Start;
            readonly HalfEdge m_End;
            HalfEdge m_Current;

            public HalfEdgeEnumerator(HalfEdge start, HalfEdge end)
            {
                m_Start = start;
                m_End = end;
                m_Current = default;
            }

            public HalfEdge Current => m_Current;

            public bool MoveNext()
            {
                if (!m_Current.GetIsValid())
                {
                    m_Current = m_Start;
                    return true;
                }

                m_Current = m_Current.GetNext();
                return m_Current != m_End;
            }
        }

        readonly struct LeftChainIterator
        {
            readonly HalfEdge m_Top;
            readonly HalfEdge m_Bottom;

            public LeftChainIterator(HalfEdge top, HalfEdge bottom)
            {
                m_Top = top;
                m_Bottom = bottom;
            }

            public HalfEdgeEnumerator GetEnumerator() => new(m_Top, m_Bottom);
        }

        readonly struct RightChainIterator
        {
            readonly HalfEdge m_Top;
            readonly HalfEdge m_Bottom;

            public RightChainIterator(HalfEdge top, HalfEdge bottom)
            {
                m_Top = top;
                m_Bottom = bottom;
            }

            public HalfEdgeEnumerator GetEnumerator() => new(m_Bottom, m_Top);
        }

        public readonly struct HalfEdgesIterator
        {
            readonly HalfEdge m_Edge;

            public HalfEdgesIterator(Face face)
            {
                m_Edge = face.GetOuterComponent();
            }

            public HalfEdgesIterator(HalfEdge edge)
            {
                m_Edge = edge;
            }

            public HalfEdgeEnumerator GetEnumerator() => new(m_Edge, m_Edge);
        }

        readonly struct HalfEdgesConnectedToVertexIterator
        {
            public struct Enumerator
            {
                readonly Vertex m_Vertex;
                HalfEdge m_Current;

                public Enumerator(Vertex vertex)
                {
                    m_Vertex = vertex;
                    m_Current = default;
                }

                public HalfEdge Current => m_Current;

                public bool MoveNext()
                {
                    if (!m_Current.GetIsValid())
                    {
                        m_Current = m_Vertex.GetIncidentEdge();
                        return true;
                    }

                    m_Current = m_Current.GetPrev().GetTwin();
                    return m_Current != m_Vertex.GetIncidentEdge();
                }
            }

            readonly Vertex m_Vertex;

            public HalfEdgesConnectedToVertexIterator(Vertex vertex)
            {
                m_Vertex = vertex;
            }

            public Enumerator GetEnumerator() => new(m_Vertex);
        }

        public readonly struct VerticesIterator
        {
            public struct Enumerator
            {
                readonly DoublyConnectedEdgeList m_Dcel;
                Vertex m_Current;
                int m_Index;

                public Enumerator(DoublyConnectedEdgeList dcel)
                {
                    m_Dcel = dcel;
                    m_Index = -1;
                    m_Current = default;
                }

                public Vertex Current => m_Current;

                public bool MoveNext()
                {
                    ++m_Index;
                    if (m_Index < m_Dcel.m_Vertices.Length)
                    {
                        m_Current = new Vertex(m_Dcel, m_Index);
                        return true;
                    }

                    return false;
                }
            }

            readonly DoublyConnectedEdgeList m_Dcel;

            public VerticesIterator(DoublyConnectedEdgeList dcel)
            {
                m_Dcel = dcel;
            }

            public Enumerator GetEnumerator() => new(m_Dcel);
        }

        public readonly struct FacesIterator
        {
            public struct Enumerator
            {
                readonly DoublyConnectedEdgeList m_Dcel;
                Face m_Current;
                int m_Index;

                public Enumerator(DoublyConnectedEdgeList dcel)
                {
                    m_Dcel = dcel;
                    m_Index = -1;
                    m_Current = default;
                }

                public Face Current => m_Current;

                public bool MoveNext()
                {
                    ++m_Index;
                    if (m_Index < m_Dcel.m_Faces.Length)
                    {
                        m_Current = new Face(m_Dcel, m_Index);
                        return true;
                    }

                    return false;
                }
            }

            readonly DoublyConnectedEdgeList m_Dcel;

            public FacesIterator(DoublyConnectedEdgeList dcel)
            {
                m_Dcel = dcel;
            }

            public Enumerator GetEnumerator() => new(m_Dcel);
        }
    }
}
