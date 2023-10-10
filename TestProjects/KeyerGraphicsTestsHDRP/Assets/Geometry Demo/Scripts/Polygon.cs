using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteInEditMode]
    public class Polygon : BasePolygon
    {
        [Flags]
        enum ViewOptions : short
        {
            None = 0,
            Edges = 1 << 0,
            Vertices = 1 << 1
        };

        [SerializeField]
        List<float2> m_Points = new();

        [SerializeField]
        Color m_GizmoColor;

        [SerializeField]
        ViewOptions m_ViewOptions;

        Action<BasePolygon> m_OnChanged = delegate {};

        void OnValidate()
        {
            if (enabled)
            {
                m_OnChanged.Invoke(this);
            }
        }

        [ContextMenu("Dispatch On Changed")]
        void DispatchOnChanged()
        {
            m_OnChanged.Invoke(this);
        }

        public override NativeArray<float2> GetVerticesCcw()
        {
            if (m_Points == null)
            {
                return default;
            }

            var points = new NativeArray<float2>(m_Points.Count, Allocator.Temp);
            for (var i = 0; i < m_Points.Count; i++)
            {
                points[i] = m_Points[i];
            }

            return points;
        }

        public override void RegisterOnChanged(Action<BasePolygon> callback) => m_OnChanged += callback;
        public override void UnregisterOnChanged(Action<BasePolygon> callback) => m_OnChanged -= callback;

        void OnDrawGizmos()
        {
            if (m_Points == null || m_ViewOptions == ViewOptions.None)
            {
                return;
            }

            Gizmos.color = m_GizmoColor;

            if (m_ViewOptions.HasFlag(ViewOptions.Edges))
            {
                for (var i = 0; i != m_Points.Count; ++i)
                {
                    Gizmos.DrawLine(m_Points[i].AsVec3(), Vector3.Lerp(m_Points[i].AsVec3(), m_Points[(i + 1) % m_Points.Count].AsVec3(), 0.9f));
                }
            }

            if (m_ViewOptions.HasFlag(ViewOptions.Vertices))
            {
                for (var i = 0; i != m_Points.Count; ++i)
                {
                    Gizmos.DrawSphere(m_Points[i].AsVec3(), 0.01f);
                }
            }
        }
    }
}
