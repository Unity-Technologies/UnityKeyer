using System;

namespace Unity.Media.Keyer.Geometry
{
    // Non exhaustive, the purpose is to feed triangulation algorithms.
    interface IDoublyConnectedEdgeList
    {
        DoublyConnectedEdgeList.VerticesIterator GetVerticesIterator();
        void SplitFace(DoublyConnectedEdgeList.HalfEdge edge, DoublyConnectedEdgeList.Vertex vertex);
        void SplitFace(DoublyConnectedEdgeList.HalfEdge left, DoublyConnectedEdgeList.HalfEdge right, EdgeAssign edgeAssign, DoublyConnectedEdgeList.Vertex vertex);
    }
}
