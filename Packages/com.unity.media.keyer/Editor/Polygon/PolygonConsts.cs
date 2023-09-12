using System;
using UnityEngine;

namespace Unity.Media.Keyer.Editor
{
    static class PolygonToolPalette
    {
        public static readonly Color Hover = Color.yellow;
        public static readonly Color Error = Color.red;
        public static readonly Color Ok = Color.green;
        public static readonly Color Select = Color.cyan;
    }

    static class PolygonToolConsts
    {
        public const float DistToPointWorld = .1f;
        public const float PointDiscSizeWorld = .05f;
        public const float PointDiscSizePixels = 5;
        public const float PointSelectionRadiusScreenSpace = 18f;
        public const float PolygonBoundsMarginScreenSpace = 32f;
        public const float DottedLineSize = 5f;
    }
}
