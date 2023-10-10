using System;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    // TODO Is there a better way to handle this?
    // We're restricted in our use of interfaces.
    public abstract class BasePolygonConsumer : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
            foreach (var polygon in GetComponents<BasePolygon>())
            {
                polygon.RegisterOnChanged(Execute);
            }
        }

        protected virtual void OnDisable()
        {
            foreach (var polygon in GetComponents<BasePolygon>())
            {
                polygon.UnregisterOnChanged(Execute);
            }
        }

        protected abstract void Execute(BasePolygon polygon);
    }
}
