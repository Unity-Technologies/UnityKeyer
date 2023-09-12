using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using CoreUtilities = Unity.Media.Keyer.Utilities;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteInEditMode]
    public class BindKeyerSettings : BasePolygon
    {
        [Flags]
        enum ViewOptions : short
        {
            None = 0,
            Edges = 1 << 0,
            Vertices = 1 << 1
        };

        [SerializeField]
        KeyerSettings m_Settings;

        [SerializeField]
        Color m_GizmoColor;

        [SerializeField]
        ViewOptions m_ViewOptions;

        Action<BasePolygon> m_OnChanged = delegate { };


        [ContextMenu("Dispatch On Changed")]
        void DispatchOnChanged()
        {
            m_OnChanged.Invoke(this);
        }

        public override NativeArray<float2> GetVerticesCcw()
        {
            if (m_Settings == null)
            {
                return default;
            }

            var pointList = m_Settings.GarbageMask.Points;
            if (pointList == null)
            {
                return default;
            }

            var points = new NativeArray<float2>(pointList.Count, Allocator.Temp);
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = pointList[i];
            }

            var order = Utilities.GetOrder(points);
            if (order == Order.ClockWise)
            {
                CoreUtilities.Reverse(points);
            }

            return points;
        }

        public override void RegisterOnChanged(Action<BasePolygon> callback) => m_OnChanged += callback;
        public override void UnregisterOnChanged(Action<BasePolygon> callback) => m_OnChanged -= callback;

        void OnDrawGizmos()
        {
            if (m_Settings == null)
            {
                return;
            }

            var srcPoints = m_Settings.GarbageMask.Points;
            if (srcPoints == null || m_ViewOptions == ViewOptions.None)
            {
                return;
            }

            Gizmos.color = m_GizmoColor;

            if (m_ViewOptions.HasFlag(ViewOptions.Edges))
            {
                for (var i = 0; i != srcPoints.Count; ++i)
                {
                    Gizmos.DrawLine(srcPoints[i], Vector3.Lerp(srcPoints[i], srcPoints[(i + 1) % srcPoints.Count], 0.9f));
                }
            }

            if (m_ViewOptions.HasFlag(ViewOptions.Vertices))
            {
                for (var i = 0; i != srcPoints.Count; ++i)
                {
                    Gizmos.DrawSphere(srcPoints[i], 0.01f);
                }
            }
        }
    }
}
