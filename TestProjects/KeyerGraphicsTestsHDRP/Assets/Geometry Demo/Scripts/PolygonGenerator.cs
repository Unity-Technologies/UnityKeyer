using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;
using GeometryUtil = Unity.Media.Keyer.Geometry.Utilities;

namespace Unity.Media.Keyer.Geometry.Demo
{
    [ExecuteInEditMode]
    public class PolygonGenerator : BasePolygon
    {
        [Flags]
        enum ViewOptions : short
        {
            None = 0,
            Edges = 1 << 0,
            Vertices = 1 << 1,
            Area = 1 << 2
        };

        [SerializeField, Range(3, 64)]
        int m_PointCount;
        [SerializeField]
        Color m_GizmoColor;
        [SerializeField]
        bool m_IsMonotone;
        [SerializeField]
        Rect m_Area;
        [SerializeField, Range(0, 256)]
        uint m_Seed;
        [SerializeField, Range(0, 1)]
        float m_NoiseAmount;
        [SerializeField, Range(.01f, 12)]
        float m_Frequency;
        [SerializeField, Range(0, 1)]
        float m_Amplitude;

        [SerializeField]
        ViewOptions m_ViewOptions;

        Action<BasePolygon> m_OnChanged = delegate {};

        NativeArray<float2> m_VerticesCcw;

        public override NativeArray<float2> GetVerticesCcw() => m_VerticesCcw;

        public override void RegisterOnChanged(Action<BasePolygon> callback) => m_OnChanged += callback;
        public override void UnregisterOnChanged(Action<BasePolygon> callback) => m_OnChanged -= callback;

        void OnEnable()
        {
            Generate();
            m_OnChanged.Invoke(this);
        }

        void OnDisable()
        {
            Media.Keyer.Utilities.DeallocateNativeArrayIfNeeded(ref m_VerticesCcw);
        }

        void OnValidate()
        {
            if (enabled)
            {
                Generate();
                m_OnChanged.Invoke(this);
            }
        }

        void Reset()
        {
            m_Area = new Rect(0, 0, 1, 1);
        }

        void Generate()
        {
            var rand = Random.CreateFromIndex(m_Seed);

            Media.Keyer.Utilities.DeallocateNativeArrayIfNeeded(ref m_VerticesCcw);

            if (m_IsMonotone)
            {
                m_VerticesCcw = GeometryUtil.CreatePolygonYMonotoneCcw(Allocator.Persistent, m_PointCount, m_Area, ref rand, m_Frequency, m_Amplitude);

                // TODO Quit instancing DCELs all the time.
                // TODO use unit tests.
                var dcel = new DoublyConnectedEdgeList();
                dcel.InitializeFromCcwVertices(m_VerticesCcw, Allocator.Temp);
                Assert.IsTrue(DoublyConnectedEdgeList.IsMonotone(dcel));
                dcel.Dispose();
            }
            else
            {
                m_VerticesCcw = GeometryUtil.CreatePolygonCcw(Allocator.Persistent, m_PointCount, m_Area, m_NoiseAmount, ref rand);
            }

            if (GeometryUtil.PolygonHasConsecutiveCollinearEdges(m_VerticesCcw))
            {
                throw new InvalidOperationException("YES");
            }
        }

        void OnDrawGizmos()
        {
            if (!m_VerticesCcw.IsCreated || m_ViewOptions == ViewOptions.None)
            {
                return;
            }

            Gizmos.color = m_GizmoColor;

            if (m_ViewOptions.HasFlag(ViewOptions.Area))
            {
                // Draw area.
                var topLeft = new Vector3(m_Area.xMin, m_Area.yMax);
                var bottomLeft = new Vector3(m_Area.xMin, m_Area.yMin);
                var bottomRight = new Vector3(m_Area.xMax, m_Area.yMin);
                var topRight = new Vector3(m_Area.xMax, m_Area.yMax);
                Gizmos.DrawLine(topLeft, bottomLeft);
                Gizmos.DrawLine(bottomLeft, bottomRight);
                Gizmos.DrawLine(bottomRight, topRight);
                Gizmos.DrawLine(topRight, topLeft);
            }

            if (m_ViewOptions.HasFlag(ViewOptions.Edges))
            {
                for (var i = 0; i != m_VerticesCcw.Length; ++i)
                {
                    Gizmos.DrawLine(m_VerticesCcw[i].AsVec3(), Vector3.Lerp(m_VerticesCcw[i].AsVec3(), m_VerticesCcw[(i + 1) % m_VerticesCcw.Length].AsVec3(), 0.9f));
                }
            }

            if (m_ViewOptions.HasFlag(ViewOptions.Vertices))
            {
                for (var i = 0; i != m_VerticesCcw.Length; ++i)
                {
                    Gizmos.DrawSphere(m_VerticesCcw[i].AsVec3(), 0.01f);
                }
            }
        }
    }
}
