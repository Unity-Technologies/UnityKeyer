using System;
using UnityEditor;

namespace Unity.Media.Keyer.Editor
{
    interface IScopeFactory
    {
        IDisposable GetScope();
    }

    readonly struct PolygonEditScope : IDisposable
    {
        readonly Keyer m_Keyer;
        readonly SerializedProperty m_PointsProperty;

        public PolygonEditScope(Keyer keyer, SerializedProperty pointsProperty)
        {
            m_Keyer = keyer;
            m_PointsProperty = pointsProperty;
        }

        public void Dispose()
        {
            m_PointsProperty.serializedObject.ApplyModifiedProperties();
            m_PointsProperty.serializedObject.Update();
            m_Keyer.RequestGarbageMaskGeneration();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    readonly struct PolygonEditScopeFactory : IScopeFactory
    {
        readonly Keyer m_Keyer;
        readonly SerializedProperty m_PointsProperty;

        public PolygonEditScopeFactory(Keyer keyer, SerializedProperty pointsProperty)
        {
            m_Keyer = keyer;
            m_PointsProperty = pointsProperty;
        }

        public IDisposable GetScope() => new PolygonEditScope(m_Keyer, m_PointsProperty);
    }
}
