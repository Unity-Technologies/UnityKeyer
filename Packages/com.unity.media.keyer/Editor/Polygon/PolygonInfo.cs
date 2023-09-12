using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Keyer.Editor
{
    // Store all polygon data relevant for tools, and controls its access.
    // Decouples the Keyer from polygon edition.
    // LocalToWorld must have uniform scaling. ImageSpaceTransform does not.
    readonly struct PolygonInfo
    {
        static readonly Matrix4x4 k_DefaultImageSpaceTransform = Matrix4x4.TRS(new Vector3(-.5f, -.5f, 0), Quaternion.identity, Vector3.one);

        readonly Matrix4x4 m_LocalToWorld;
        readonly Matrix4x4 m_ImageToWorld;
        readonly Matrix4x4 m_WorldToImage;
        readonly Matrix4x4 m_ImageSpaceTransform;
        readonly Vector3 m_Forward;
        readonly Vector3 m_Position;
        readonly SerializedProperty m_PointsProperty;
        readonly IScopeFactory m_ScopeFactory;

        public PolygonInfo(SerializedProperty pointsProperty, IScopeFactory scopeFactory, Transform transform)
        {
            m_PointsProperty = pointsProperty;
            m_ScopeFactory = scopeFactory;
            m_LocalToWorld = transform.localToWorldMatrix;
            m_Forward = transform.forward;
            m_Position = transform.position;
            m_ImageSpaceTransform = k_DefaultImageSpaceTransform;
            m_ImageToWorld = m_LocalToWorld * m_ImageSpaceTransform;
            m_WorldToImage = m_ImageToWorld.inverse;
        }

        public PolygonInfo(SerializedProperty pointsProperty, IScopeFactory scopeFactory, Matrix4x4 localToWorldMatrix, Matrix4x4 imageSpaceTransform, Vector3 forward, Vector3 position)
        {
            m_PointsProperty = pointsProperty;
            m_ScopeFactory = scopeFactory;

            m_LocalToWorld = localToWorldMatrix;
            m_Forward = forward;
            m_Position = position;
            m_ImageSpaceTransform = imageSpaceTransform;

            m_ImageToWorld = m_LocalToWorld * m_ImageSpaceTransform;
            m_WorldToImage = m_ImageToWorld.inverse;
        }

        public bool IsValid => m_PointsProperty != null;

        public IDisposable CreateEditScope() => m_ScopeFactory.GetScope();

        public Vector3 Normal => m_Forward;

        public Plane Plane => new(Normal, m_Position);

        public int GetNumPoints() => m_PointsProperty.arraySize;

        public Vector2 GetPointAtIndex(int index) => m_PointsProperty.GetArrayElementAtIndex(index).vector2Value;

        public void InsertPointAtIndex(int index, Vector2 point)
        {
            m_PointsProperty.InsertArrayElementAtIndex(index);
            m_PointsProperty.GetArrayElementAtIndex(index).vector2Value = point;
        }

        public void DeletePointAtIndex(int index)
        {
            m_PointsProperty.DeleteArrayElementAtIndex(index);
        }

        public void CopyPoints(List<Vector2> points)
        {
            for (var i = 0; i != GetNumPoints(); ++i)
            {
                points.Add(GetPointAtIndex(i));
            }
        }

        public void SetPointAtIndex(int index, Vector2 value)
        {
            var elt = m_PointsProperty.GetArrayElementAtIndex(index);
            elt.vector2Value = value;
        }

        public void AppendPoint(Vector2 point) => InsertPointAtIndex(GetNumPoints(), point);

        public void SwapPointsAtIndices(int indexA, int indexB)
        {
            var eltA = m_PointsProperty.GetArrayElementAtIndex(indexA);
            var eltB = m_PointsProperty.GetArrayElementAtIndex(indexB);
            var tmp = eltA.vector2Value;
            eltA.vector2Value = eltB.vector2Value;
            eltB.vector2Value = tmp;
        }

        public Vector3 GetImageToWorldPoint(Vector3 position) => m_ImageToWorld * MakeVector4(position, 1);

        public Vector3 GetWorldToImagePoint(Vector3 position) => m_WorldToImage * MakeVector4(position, 1);

        // For repaint.
        public Vector3 GetImageToLocalPoint(Vector3 position) => m_ImageSpaceTransform * MakeVector4(position, 1);

        // For repaint. Needs uniform scaled matrix to preserve handles aspect.
        public Matrix4x4 LocalToWorld => m_LocalToWorld;

        // Shorthands.
        public Vector3 GetImageToWorldPoint(int index) => GetImageToWorldPoint(GetPointAtIndex(index));

        public Vector3 GetImageToLocalPoint(int index) => GetImageToLocalPoint(GetPointAtIndex(index));

        // Utility.
        static Vector4 MakeVector4(Vector3 vec, float w) => new(vec.x, vec.y, vec.z, w);
    }
}
