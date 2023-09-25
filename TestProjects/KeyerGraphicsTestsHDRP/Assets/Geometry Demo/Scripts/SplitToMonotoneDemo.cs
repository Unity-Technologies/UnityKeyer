using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteAlways]
    public class SplitToMonotoneDemo : BasePolygonConsumer
    {
        [SerializeField, Range(0.01f, 1)]
        float m_Delay;
        [SerializeField]
        SplitToMonotoneGizmos.ViewOptions m_ViewOptions;

        readonly DoublyConnectedEdgeList m_Dcel = new();
        readonly SplitToMonotoneDoublyConnectedEdgeListMemento m_Memento = new();
        readonly SplitToMonotone.SweepStatus m_SweepStatus = new();
        readonly Dictionary<int, VertexType> m_VerticesClassification = new();
        SplitToMonotoneDoublyConnectedEdgeListMemento.Snapshot m_Snapshot;

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
                SplitToMonotoneGizmos.Draw(m_ViewOptions, m_Dcel, m_Memento, m_Snapshot, m_VerticesClassification);
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

                // FromCcwVertices puts the inner face at index 0.
                m_Memento.Initialize(m_Dcel, m_SweepStatus);
                SplitToMonotone.Execute(m_Memento, m_Dcel.GetInnerFace(), m_SweepStatus, m_VerticesClassification);

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
