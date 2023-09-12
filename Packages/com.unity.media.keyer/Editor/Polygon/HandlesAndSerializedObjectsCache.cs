using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Editor
{
    class HandlesAndSerializedObjectsCache
    {
        readonly Dictionary<Object, PolygonHandles> m_PolygonHandles = new();
        readonly Dictionary<Object, SerializedObject> m_SerializedObjects = new();

        public PolygonHandles GetHandles(Object obj)
        {
            if (!m_PolygonHandles.TryGetValue(obj, out var handles))
            {
                handles = new PolygonHandles();
                m_PolygonHandles.Add(obj, handles);
            }

            return handles;
        }

        public bool TryGetPointsSerializedProperty(Keyer keyer, out SerializedProperty property)
        {
            var obj = keyer.Settings;
            if (obj == null)
            {
                property = null;
                return false;
            }

            SerializedObject so;

            if (!m_SerializedObjects.TryGetValue(obj, out so))
            {
                so = new SerializedObject(obj);
                m_SerializedObjects.Add(obj, so);
            }

            so.Update();
            property = so.FindProperty("m_GarbageMask").FindPropertyRelative("m_Points");
            return true;
        }

        public void Clear()
        {
            m_SerializedObjects.Clear();
            m_PolygonHandles.Clear();
        }
    }
}
