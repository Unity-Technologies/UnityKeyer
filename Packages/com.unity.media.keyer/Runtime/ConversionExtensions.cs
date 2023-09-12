using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer
{
    static class ConversionExtensions
    {
        public static Color ToColor(this Vector3 value)
        {
            return new Color(value.x, value.y, value.z);
        }

        public static Color ToColor(this float3 value)
        {
            return new Color(value.x, value.y, value.z);
        }

        public static Vector3 ToVector3(this Color value)
        {
            return new Vector3(value.r, value.g, value.b);
        }

        public static float3 ToFloat3(this Color value)
        {
            return new float3(value.r, value.g, value.b);
        }
    }
}
