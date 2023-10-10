using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteAlways]
    [RequireComponent(typeof(PolygonGenerator))]
    public class TriangulateMonotoneDemo : BasePolygonConsumer
    {
        [SerializeField, Range(0.01f, 1)]
        float m_Delay;
        [SerializeField]
        TriangulateMonotoneGizmos.ViewOptions m_ViewOptions;

        readonly DoublyConnectedEdgeList m_Dcel = new();
        readonly TriangulateMonotone.VertexStack m_VerticesStack = new();
        readonly TriangulateMonotoneDoublyConnectedEdgeListMemento m_Memento = new();
        TriangulateMonotoneDoublyConnectedEdgeListMemento.Snapshot m_Snapshot;


        protected override void OnDisable()
        {
            StopAllCoroutines();
            m_Dcel.Dispose();
            base.OnDisable();
        }

        void OnDrawGizmos()
        {
            if (m_Memento != null)
            {
                TriangulateMonotoneGizmos.Draw(m_ViewOptions, m_Memento, m_Snapshot);
            }
        }

        protected override void Execute(BasePolygon polygon)
        {
            StopAllCoroutines();
            StartCoroutine(Run(polygon));
        }

        IEnumerator Run(BasePolygon polygon)
        {
            var verticesCcw = polygon.GetVerticesCcw();
            if (verticesCcw.IsCreated)
            {
                m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Persistent);
                m_Memento.Initialize(m_Dcel, m_VerticesStack);

                // FromCcwVertices puts the inner face at index 0.
                TriangulateMonotone.Execute(m_Memento, m_Dcel.GetInnerFace(), m_VerticesStack);

                // Playback for visualization.
                foreach (var snapshot in m_Memento.Snapshots)
                {
                    m_Snapshot = snapshot;

                    yield return new WaitForSeconds(m_Delay);
                }
            }
            else
            {
                Debug.LogError("Failed to read polygon vertices.");
            }
        }
    }
}
