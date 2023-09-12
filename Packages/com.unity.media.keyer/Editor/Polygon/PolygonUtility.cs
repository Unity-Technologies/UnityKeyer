using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GeometryUtil = Unity.Media.Keyer.Geometry.Utilities;

namespace Unity.Media.Keyer.Editor
{
    static class PolygonUtility
    {
        struct HandlesInfo
        {
            public bool UseCamera;
            public Matrix4x4 Matrix;

            public static HandlesInfo GetDefault()
            {
                return new()
                {
                    UseCamera = true,
                    Matrix = Matrix4x4.identity
                };
            }
        }

        readonly struct HandlesCameraScope : IDisposable
        {
            public HandlesCameraScope(bool useCamera, Matrix4x4 matrix)
            {
                s_HandlesInfo.Push(new HandlesInfo
                {
                    UseCamera = useCamera,
                    Matrix = matrix
                });
            }

            public void Dispose()
            {
                s_HandlesInfo.Pop();
            }
        }

        static readonly Vector3[] s_NormalizedBounds =
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(1, 0, 0),
            new(1, 1, 0),
            new(1, 1, 0),
            new(0, 1, 0),
            new(0, 1, 0),
            new(0, 0, 0)
        };

        static readonly Stack<HandlesInfo> s_HandlesInfo = new();

        // Caches.
        static readonly Vector3[] s_ScreenSpaceBounds = new Vector3[4];
        static readonly Vector3[] s_ImageSpaceBounds = new Vector3[8];

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            s_HandlesInfo.Clear();
            s_HandlesInfo.Push(HandlesInfo.GetDefault());
        }

        public static IDisposable GetHandlesCameraScope(bool useCamera, Matrix4x4 matrix) => new HandlesCameraScope(useCamera, matrix);

        static int FindClosestPoint(PolygonInfo info, Vector2 mousePosition, out float distance)
        {
            if (TryScreenToImagePosition(info, mousePosition, out var position))
            {
                var minDist = float.MaxValue;
                var index = -1;

                var numPoints = info.GetNumPoints();
                for (var i = 0; i != numPoints; i++)
                {
                    var dist = Vector2.Distance(info.GetPointAtIndex(i), position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        index = i;
                    }
                }

                distance = minDist;
                return index;
            }

            distance = float.MaxValue;
            return -1;
        }

        // Screen Space.
        public static float DistanceToClosestSegment(PolygonInfo info, Vector2 mousePosition, out int closestOriginIndex)
        {
            var numPoints = info.GetNumPoints();

            if (numPoints < 2)
            {
                closestOriginIndex = -1;
                return float.MaxValue;
            }

            closestOriginIndex = 0;
            var minDist = float.MaxValue;

            var origin = WorldToGUIPoint(
                info.GetImageToWorldPoint(
                    info.GetPointAtIndex(0)));

            for (var i = 0; i != numPoints; ++i)
            {
                var destination = WorldToGUIPoint(
                    info.GetImageToWorldPoint(
                        info.GetPointAtIndex((i + 1) % numPoints)));
                var dist = GeometryUtil.DistanceToSegment(mousePosition, origin, destination);

                if (dist < minDist)
                {
                    closestOriginIndex = i;
                    minDist = dist;
                }

                // Do not re-compute transformed point.
                origin = destination;
            }

            return minDist;
        }

        public static int FindClosestPoint(PolygonInfo info, Vector2 mousePosition, out float distanceImage, out float distanceScreen)
        {
            var index = FindClosestPoint(info, mousePosition, out distanceImage);
            if (index == -1)
            {
                distanceScreen = float.MaxValue;
                return index;
            }

            var worldPosition = info.GetImageToWorldPoint(index);

            var guiPosition = WorldToGUIPoint(worldPosition);
            distanceScreen = (mousePosition - guiPosition).magnitude;
            return index;
        }

        public static bool TryScreenToImagePosition(PolygonInfo info, Vector2 mousePosition, out Vector3 localPosition)
        {
            if (TryScreenToWorldPosition(info, mousePosition, out var worldPosition))
            {
                localPosition = info.GetWorldToImagePoint(worldPosition);
                return true;
            }

            localPosition = Vector3.zero;
            return false;
        }

        public static bool TryScreenToWorldPosition(PolygonInfo info, Vector2 mousePosition, out Vector3 worldPosition)
        {
            var ray = GUIPointToWorldRay(mousePosition);

            if (info.Plane.Raycast(ray, out var distance))
            {
                worldPosition = ray.origin + ray.direction * distance;
                return true;
            }

            worldPosition = Vector3.zero;
            return false;
        }

        public static bool TryScreenToImageVector(PolygonInfo info, Vector2 mousePosition, Vector2 mouseDelta, out Vector3 delta)
        {
            var valid = true;
            valid &= TryScreenToImagePosition(info, mousePosition - mouseDelta, out var from);
            valid &= TryScreenToImagePosition(info, mousePosition, out var to);

            if (!valid)
            {
                delta = Vector3.zero;
                return false;
            }

            delta = to - from;
            return true;
        }

        public static bool TryScreenToImageInsertion(PolygonInfo info, Vector2 mousePosition, out int insertionIndex, out Vector3 position)
        {
            if (info.GetNumPoints() < 2)
            {
                insertionIndex = -1;
                position = Vector3.zero;
                return false;
            }

            if (TryScreenToImagePosition(info, mousePosition, out position))
            {
                var numPoints = info.GetNumPoints();
                var minDist = float.MaxValue;
                var index = -1;

                for (var i = 0; i != numPoints; ++i)
                {
                    var dist = HandleUtility.DistancePointLine(
                        position, info.GetPointAtIndex(i), info.GetPointAtIndex((i + 1) % numPoints));
                    if (dist < minDist)
                    {
                        minDist = dist;
                        index = i;
                    }
                }

                insertionIndex = index;
                return true;
            }

            insertionIndex = -1;
            position = Vector3.zero;
            return false;
        }

        public static float DistanceToPolygon(PolygonInfo info)
        {
            var numPoints = info.GetNumPoints();
            if (numPoints == 1)
            {
                // To avoid errors related to the CameraProjectionCache,
                // we evaluate the distance to the point as the distance to a line formed by the point and a slightly nudged duplicate.
                var point = info.GetImageToWorldPoint(0);
                return HandleUtility.DistanceToLine(point, point + Vector3.one * Mathf.Epsilon);
            }

            using var worldPoints = ScopedList<Vector3>.Create();

            for (var i = 0; i != numPoints; i++)
            {
                worldPoints.List.Add(info.GetImageToWorldPoint(i));
            }

            var dist = float.MaxValue;

            for (var i = 0; i != worldPoints.List.Count; i++)
            {
                var a = worldPoints.List[i];
                var b = worldPoints.List[(i + 1) % worldPoints.List.Count];
                dist = Mathf.Min(HandleUtility.DistanceToLine(a, b), dist);
            }

            return dist;
        }

        public static bool GetGeometryScreenSpaceBounds(PolygonInfo info, out Rect rect)
        {
            var numPoints = info.GetNumPoints();
            if (numPoints == 0)
            {
                rect = Rect.zero;
                return false;
            }

            var initPosition = WorldToGUIPoint(info.GetImageToWorldPoint(0));
            rect = new Rect(initPosition, Vector2.zero);

            for (var i = 1; i != numPoints; ++i)
            {
                var position = WorldToGUIPoint(info.GetImageToWorldPoint(i));
                rect = GeometryUtil.Expand(rect, position);
            }

            return true;
        }

        public static Rect GetScreenSpaceBounds(PolygonInfo info)
        {
            for (var i = 0; i != s_ScreenSpaceBounds.Length; ++i)
            {
                s_ScreenSpaceBounds[i] = WorldToGUIPoint(info.GetImageToWorldPoint(s_NormalizedBounds[i]));
            }

            var rect = new Rect(s_ScreenSpaceBounds[0], Vector2.zero);

            for (var i = 1; i != s_ScreenSpaceBounds.Length; ++i)
            {
                rect = GeometryUtil.Expand(rect, s_ScreenSpaceBounds[i]);
            }

            return rect;
        }

        public static void DrawImageSpaceBounds(PolygonInfo info)
        {
            using var drawingScope = new Handles.DrawingScope(Color.white, info.LocalToWorld);

            for (var i = 0; i != s_ImageSpaceBounds.Length; ++i)
            {
                s_ImageSpaceBounds[i] = info.GetImageToLocalPoint(s_NormalizedBounds[i]);
            }

            Handles.DrawLines(s_ImageSpaceBounds);
        }

        // If we had lots of segments we may want to look into drawing a mesh.
        public static void Draw(PolygonInfo info)
        {
            using var drawingScope = new Handles.DrawingScope(Color.white, info.LocalToWorld);

            // Draw points.
            var numPoints = info.GetNumPoints();
            for (var i = 0; i != numPoints; i++)
            {
                DrawPointHandle(info.GetImageToLocalPoint(i), info.Normal);
            }

            if (numPoints < 2)
            {
                return;
            }

            // Draw edges.
            for (var i = 0; i != numPoints; i++)
            {
                Handles.DrawLine(
                    info.GetImageToLocalPoint(i),
                    info.GetImageToLocalPoint((i + 1) % numPoints));
            }
        }

        public static void DrawPointHandle(Vector3 position, Vector3 normal) => Handles.DrawSolidDisc(position, normal, GetPointRadius(position));

        static float GetPointRadius(Vector3 position)
        {
            if (s_HandlesInfo.Peek().UseCamera)
            {
                return HandleUtility.GetHandleSize(position) * PolygonToolConsts.PointDiscSizeWorld;
            }

            return PolygonToolConsts.PointDiscSizePixels / s_HandlesInfo.Peek().Matrix.m00;
        }

        public static Vector2 WorldToGUIPoint(Vector3 position)
        {
            if (s_HandlesInfo.Peek().UseCamera)
            {
                return HandleUtility.WorldToGUIPoint(position);
            }

            return position;
        }

        static Ray GUIPointToWorldRay(Vector2 mousePosition)
        {
            if (s_HandlesInfo.Peek().UseCamera)
            {
                return HandleUtility.GUIPointToWorldRay(mousePosition);
            }

            return new Ray((Vector3)mousePosition + Vector3.back, Vector3.forward);
        }
    }
}
