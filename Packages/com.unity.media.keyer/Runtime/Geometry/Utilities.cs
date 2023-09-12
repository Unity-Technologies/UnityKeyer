using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using CoreUtilities = Unity.Media.Keyer.Utilities;
using Random = Unity.Mathematics.Random;

namespace Unity.Media.Keyer.Geometry
{
    class Utilities
    {
        static readonly PolygonRenderer s_PolygonRenderer = new();

        public static Order GetOrder(NativeArray<float2> vertices)
        {
            var sum = 0f;
            var len = vertices.Length;
            for (var i = 0; i != len; ++i)
            {
                var p1 = vertices[i];
                var p2 = vertices[(i + 1) % len];
                sum += (p2.x - p1.x) * (p2.y + p1.y);
            }

            // Handled out of due diligence but should never be encountered in practice.
            if (sum == 0)
            {
                return Order.None;
            }

            return sum > 0 ? Order.ClockWise : Order.CounterClockWise;
        }

        public static bool Contains(Rect rect, List<Vector2> points)
        {
            for (var i = 0; i != points.Count; ++i)
            {
                if (!rect.Contains(points[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static Vector2 Clamp(Rect rect, Vector2 point)
        {
            point.x = math.clamp(point.x, rect.xMin, rect.xMax);
            point.y = math.clamp(point.y, rect.yMin, rect.yMax);
            return point;
        }

        public static bool HasSelfIntersection(List<Vector2> points)
        {
            var count = points.Count;
            if (count < 4)
            {
                return false;
            }

            for (var i = 0; i < count - 1; ++i)
            {
                for (var j = i + 2; j < count; ++j)
                {
                    if (i == 0 && j == count - 1)
                    {
                        continue;
                    }

                    if (SegmentsIntersect(
                        points[i], points[i + 1],
                        points[j], points[(j + 1) % count]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void RemoveDuplicatesAndCollinear(List<Vector2> points, float epsilon)
        {
            while (points.Count > 3)
            {
                var rmIndex = -1;
                var count = points.Count;
                for (var i = 0; i != count; ++i)
                {
                    var pt = points[i];
                    var next = points[(i + 1) % count];

                    // Remove duplicates.
                    if (Mathf.Abs(pt.x - next.x) < epsilon && Mathf.Abs(pt.y - next.y) < epsilon)
                    {
                        rmIndex = i;
                        break;
                    }

                    // Remove collinear.
                    var prev = points[(i + count - 1) % count];
                    var dPrev = pt - prev;
                    var dNext = next - pt;
                    if (Vector2.Angle(dPrev, dNext) * Mathf.Deg2Rad < epsilon)
                    {
                        rmIndex = i;
                        break;
                    }
                }

                // Nothing left to do, exit.
                if (rmIndex == -1)
                {
                    break;
                }

                points.RemoveAt(rmIndex);
            }
        }

        static bool SegmentsIntersect(float2 startA, float2 endA, float2 startB, float2 endB)
        {
            var a = endA - startA;
            var b = startB - endB;
            var c = startA - startB;

            // 2D cross products.
            var alphaNumerator = b.y * c.x - b.x * c.y;
            var betaNumerator = a.x * c.y - a.y * c.x;
            var denominator = a.y * b.x - a.x * b.y;

            if (denominator == 0)
            {
                return false;
            }

            if (denominator > 0)
            {
                if (math.min(alphaNumerator, betaNumerator) < 0 ||
                    math.max(alphaNumerator, betaNumerator) > denominator)
                {
                    return false;
                }
            }
            else if (math.max(alphaNumerator, betaNumerator) > 0 ||
                math.min(alphaNumerator, betaNumerator) < denominator)
            {
                return false;
            }

            return true;
        }

        public static float DistanceToSegment(float2 p, float2 p1, float2 p2)
        {
            var center = (p1 + p2) * 0.5f;
            var len = math.length(p2 - p1);
            var dir = (p2 - p1) / len;
            var relP = p - center;
            var dist1 = math.abs(math.dot(relP, new float2(dir.y, -dir.x)));
            var dist2 = math.abs(math.dot(relP, dir)) - 0.5f * len;
            return math.max(dist1, dist2);
        }

        // TODO Keep this?
        // TODO Could be a job in case of large data.
        public static Bounds GetBounds(NativeArray<float3> vertices)
        {
            var min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new float3(float.MinValue, float.MinValue, float.MinValue);

            for (var i = 0; i != vertices.Length; ++i)
            {
                var vert = vertices[i];
                min = math.min(min, vert);
                max = math.max(max, vert);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        // Expand rect so that it contains the point.
        public static Rect Expand(Rect rect, Vector2 point)
        {
            rect.xMin = Mathf.Min(rect.xMin, point.x);
            rect.xMax = Mathf.Max(rect.xMax, point.x);
            rect.yMin = Mathf.Min(rect.yMin, point.y);
            rect.yMax = Mathf.Max(rect.yMax, point.y);
            return rect;
        }

        // Expand rect by a margin.
        public static Rect Expand(Rect rect, float margin)
        {
            rect.xMin -= margin;
            rect.xMax += margin;
            rect.yMin -= margin;
            rect.yMax += margin;
            return rect;
        }

        public static byte[] EncodeToPNG(List<Vector2> points, Vector2Int size)
        {
            s_PolygonRenderer.Initialize();

            var cmd = CommandBufferPool.Get("Garbage Mask Offline");

            var target = default(RenderTexture);
            CoreUtilities.AllocateIfNeededForCompute(ref target, size.x, size.y, GraphicsFormat.R8_UNorm);

            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.clear);
            s_PolygonRenderer.Render(cmd, points);

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            var previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(size.x, size.y, TextureFormat.R8, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            RenderTexture.active = previousActive;

            CoreUtilities.DeallocateIfNeeded(target);

            s_PolygonRenderer.Dispose();

            return texture.EncodeToPNG();
        }

        /// <summary>
        /// Checks whether or not vertices are sorted top to bottom.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <returns>Returns true if vertices are sorted top to bottom, false otherwise.</returns>
        public static bool CheckVerticesSweepOrder(DoublyConnectedEdgeList.Vertex[] vertices)
        {
            var x = float.MinValue;
            var y = float.MaxValue;
            for (var i = 0; i != vertices.Length; ++i)
            {
                var vx = vertices[i].GetX();
                var vy = vertices[i].GetY();
                if (!(y > vy || (y == vy && x > vx)))
                {
                    return false;
                }

                x = vx;
                y = vy;
            }

            return true;
        }

        /// <summary>
        /// Validates that all but the outer face of the geometry are triangles.
        /// </summary>
        /// <param name="dcel">The geometry as a doubly connected edge list.</param>
        /// <returns>Return true if the geometry is fully triangulated, false otherwise.</returns>
        public static bool IsFullyTriangulated(DoublyConnectedEdgeList dcel)
        {
            var checkedFaces = 0;

            foreach (var face in dcel.GetFacesIterator())
            {
                if (face.Index == dcel.OuterFaceIndex)
                {
                    continue;
                }

                var numHalfEdges = 0;
                foreach (var _ in new DoublyConnectedEdgeList.HalfEdgesIterator(face))
                {
                    ++numHalfEdges;
                }

                if (numHalfEdges != 3)
                {
                    return false;
                }

                ++checkedFaces;
            }

            return checkedFaces > 0;
        }

        /// <summary>
        /// Creates a counter clockwise polygon.
        /// </summary>
        /// <param name="allocator">The allocator used for the returned collection.</param>
        /// <param name="numPoints">The number of points of the polygon.</param>
        /// <param name="bounds">The bounds within which the polygon is contained.</param>
        /// <param name="noise">The normalized amount of noise applied to points positions.</param>
        /// <param name="random">The random generator used during polygon generation.</param>
        /// <returns>The polygon as a collection of counter clockwise 2D points.</returns>
        public static NativeArray<float2> CreatePolygonCcw(Allocator allocator, int numPoints, Rect bounds, float noise, ref Random random)
        {
            // Evaluate angles.
            var angles = new float[numPoints];
            for (var i = 0; i != numPoints; ++i)
            {
                angles[i] = random.NextFloat();
            }

            Array.Sort(angles);

            // Evaluate points.
            var center = (float2)bounds.center;
            var result = new NativeArray<float2>(numPoints, allocator);
            var dx = bounds.width * .5f;
            var dy = bounds.height * .5f;
            var lerpMin = Mathf.Clamp01(1 - noise);
            for (var i = 0; i != numPoints; ++i)
            {
                var direction = Rotate(Vector2.up, angles[i] * 2 * math.PI);
                var maxScale = math.min(dx / math.abs(direction.x), dy / math.abs(direction.y));
                result[i] = center + direction * maxScale * math.lerp(lerpMin, 1, random.NextFloat());
            }

            return result;
        }

        // Dubious name. We use this function to play with the distribution of uniform samples,
        // to get concentrations around some areas of the interval.
        // The end goal is to generate monotone polygons with pronounced funnels.
        static float Wiggle(float value, float freq, float amplitude, float offset)
        {
            return (amplitude / math.PI) * math.sin(2 * math.PI * freq * (value + offset / freq)) * math.sin(math.PI * value) + value;
        }

        /// <summary>
        /// Creates a polygon monotone along the Y axis.
        /// </summary>
        /// <param name="allocator">The allocator used for the returned collection.</param>
        /// <param name="numPoints">The number of points of the polygon.</param>
        /// <param name="bounds">The bounds within which the polygon is contained.</param>
        /// <param name="random">The random generator used during polygon generation.</param>
        /// <param name="freq">The frequency of the noise applied to points positions.</param>
        /// <param name="amplitude">The amplitude of the noise applied to points positions.</param>
        /// <returns>The polygon as a collection of counter clockwise 2D points.</returns>
        public static NativeArray<float2> CreatePolygonYMonotoneCcw(Allocator allocator, int numPoints, Rect bounds, ref Random random, float freq = 1, float amplitude = 0)
        {
            // We generate 2 chains of vertices, guaranteeing monotonicity.
            // Pick a top and bottom vertex on the top and bottom bounds edges.
            var xMid = bounds.xMin + bounds.width * .5f;
            var topVertex = new float2(xMid, bounds.yMax);
            var bottomVertex = new float2(xMid, bounds.yMin);

            // Evaluate the y coordinates we will use.
            var leftChainCount = (numPoints - 2) / 2;
            var rightChainCount = numPoints - 2 - leftChainCount;
            var yLeft = new NativeArray<float>(leftChainCount, Allocator.Temp);
            var yRight = new NativeArray<float>(rightChainCount, Allocator.Temp);
            var offset = random.NextFloat();
            for (var i = 0; i != leftChainCount; ++i)
            {
                // Avoid lying on top or bottom edges.
                var rnd = Wiggle(random.NextFloat(), freq, amplitude, offset);
                yLeft[i] = bounds.yMin + math.lerp(.01f, .99f * bounds.yMax, rnd) * bounds.height;
            }

            // Recalculate offset to avoid both chains being too similar.
            offset = random.NextFloat();
            for (var i = 0; i != rightChainCount; ++i)
            {
                // Avoid lying on top or bottom edges.
                var rnd = Wiggle(random.NextFloat(), freq, amplitude, offset);
                yRight[i] = bounds.yMin + math.lerp(.01f, .99f * bounds.yMax, rnd) * bounds.height;
            }

            yLeft.Sort();
            yRight.Sort();

            // We go in decreasing y order for the left chain.
            CoreUtilities.Reverse(yLeft);

            var result = new NativeArray<float2>(numPoints, allocator);
            result[0] = topVertex;
            var index = 0;

            // Create left chain.
            var xrnd = random.NextFloat();
            for (var i = 0; i != leftChainCount; ++i)
            {
                xrnd = math.frac(xrnd + random.NextFloat() * 0.1f);

                result[++index] = new float2(math.lerp(bounds.xMin, bounds.xMax * .49f, xrnd), yLeft[i]);
            }

            // We've reached the bottom vertex.
            result[++index] = bottomVertex;

            // Create right chain.
            for (var i = 0; i != rightChainCount; ++i)
            {
                xrnd = math.frac(xrnd + random.NextFloat() * 0.1f);

                result[++index] = new float2(math.lerp(bounds.xMax * .51f, bounds.xMax, xrnd), yRight[i]);
            }

            return result;
        }

        /// <summary>
        /// Check if a polygon has 2 consecutive collinear edges.
        /// </summary>
        /// <param name="points">The polygon as a collection of counter clockwise 2D points.</param>
        /// <returns>If true, the polygon has at least 2 consecutive collinear edges.</returns>
        public static bool PolygonHasConsecutiveCollinearEdges(NativeArray<float2> points)
        {
            for (var i = 0; i != points.Length - 1; ++i)
            {
                var dir0 = points[i + 1] - points[i];
                var dir1 = points[(i + 2) % points.Length] - points[i + 1];
                if (Vector2.Angle(dir0, dir1) < 1e-3f)
                {
                    return true;
                }
            }

            return false;
        }

        static float2 Rotate(float2 v, float angle)
        {
            var sin = math.sin(angle);
            var cos = math.cos(angle);
            return new float2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }
    }
}
