using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    // It's sad we have to resort to an abstract class, should've been an interface.
    public abstract class BasePolygon : MonoBehaviour
    {
        public abstract NativeArray<float2> GetVerticesCcw();
        public abstract void RegisterOnChanged(Action<BasePolygon> callback);
        public abstract void UnregisterOnChanged(Action<BasePolygon> callback);
    }
}
