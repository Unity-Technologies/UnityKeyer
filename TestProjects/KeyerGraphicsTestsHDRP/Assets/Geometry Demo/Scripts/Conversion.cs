using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer.Geometry.Demo
{
    static class Conversion
    {
        public static Vector3 AsVec3(this float2 x) => new(x.x, x.y, 0);

        public static bool IsDefault(this float2 x)
        {
            var eq = x == default;
            return eq.x && eq.y;
        }

        public static bool IsDefault(this float2x2 x) => x.c0.IsDefault() && x.c1.IsDefault();
    }
}
