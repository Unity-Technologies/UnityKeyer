using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Media.Keyer.Geometry;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;
using Random = Unity.Mathematics.Random;
using GeometryUtil = Unity.Media.Keyer.Geometry.Utilities;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class GeometryTests
    {
        readonly DoublyConnectedEdgeList m_Dcel = new();
        static Random s_Random;

        [SetUp]
        public void Setup()
        {
            s_Random = Random.CreateFromIndex(0);
        }

        [TearDown]
        public void TearDown()
        {
            m_Dcel.Dispose();
        }

        static IEnumerable MonotonePolygonGenerationDataSource
        {
            get
            {
                s_Random = Random.CreateFromIndex(0);

                for (var i = 0; i != 24; ++i)
                {
                    var numPoints = s_Random.NextInt(12, 48);
                    var frequency = s_Random.NextFloat(1, 5);
                    var amplitude = s_Random.NextFloat(.1f, 1);
                    yield return new TestCaseData(numPoints, frequency, amplitude);
                }
            }
        }

        // We test the polygon generation procedures we use in other tests.
        [Test]
        [TestCaseSource(nameof(MonotonePolygonGenerationDataSource))]
        public void GeneratedPolygonIsMonotone(int numPoints, float frequency, float amplitude)
        {
            var verticesCcw = GeometryUtil.CreatePolygonYMonotoneCcw(
                Allocator.Temp, numPoints, new Rect(0, 0, 1, 1), ref s_Random, frequency, amplitude);
            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);
            Assert.IsTrue(DoublyConnectedEdgeList.IsMonotone(m_Dcel));
        }

        [Test]
        [TestCaseSource(nameof(MonotonePolygonGenerationDataSource))]
        public void TriangulateMonotonePolygon(int numPoints, float frequency, float amplitude)
        {
            var verticesCcw = GeometryUtil.CreatePolygonYMonotoneCcw(
                Allocator.Temp, numPoints, new Rect(0, 0, 1, 1), ref s_Random, frequency, amplitude);
            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);
            TriangulateMonotone.Execute(m_Dcel, m_Dcel.GetInnerFace());
            Assert.IsTrue(GeometryUtil.IsFullyTriangulated(m_Dcel));
        }

        // We check sorting for it is done by merging chains to be more efficient.
        [Test]
        [TestCaseSource(nameof(MonotonePolygonGenerationDataSource))]
        public void SweepVerticesAreSortedCorrectly(int numPoints, float frequency, float amplitude)
        {
            var verticesCcw = GeometryUtil.CreatePolygonYMonotoneCcw(
                Allocator.Temp, numPoints, new Rect(0, 0, 1, 1), ref s_Random, frequency, amplitude);
            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);

            var face = m_Dcel.GetInnerFace();
            var vertices = new DoublyConnectedEdgeList.Vertex[DoublyConnectedEdgeList.CountHalfEdges(face)];
            DoublyConnectedEdgeList.SortSweepMonotone(vertices, face);

            Assert.IsTrue(GeometryUtil.CheckVerticesSweepOrder(vertices));
        }

        static IEnumerable PolygonGenerationDataSource
        {
            get
            {
                s_Random = Random.CreateFromIndex(0);
                var bounds = new Rect(0, 0, 1, 1);

                for (var i = 0; i != 24; ++i)
                {
                    var numPoints = s_Random.NextInt(12, 48);
                    var noise = s_Random.NextFloat();
                    yield return new TestCaseData(numPoints, bounds, noise);
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(PolygonGenerationDataSource))]
        public void SplitToMonotoneTest(int numPoints, Rect bounds, float noise)
        {
            // TODO GeometryUtil should use native type as well.
            var verticesCcw = GeometryUtil.CreatePolygonCcw(Allocator.Temp, numPoints, bounds, noise, ref s_Random);
            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);
            SplitToMonotone.Execute(m_Dcel, m_Dcel.GetInnerFace());

            // We check that each face is monotone.
            foreach (var face in m_Dcel.GetFacesIterator())
            {
                if (face.Index == m_Dcel.OuterFaceIndex)
                {
                    continue;
                }

                Assert.IsTrue(DoublyConnectedEdgeList.IsMonotone(face));
            }
        }

        // Axis aligned right angles can be problematic.
        // Strictly speaking: start, end, merge and split vertices are tricky to identify and require a convention.
        [Test]
        public void SplitToMonotoneSquare()
        {
            // TODO GeometryUtil should use native type as well.
            var verticesCcw = new NativeArray<float2>(4, Allocator.Temp);
            verticesCcw[0] = new float2(0, 0);
            verticesCcw[1] = new float2(1, 0);
            verticesCcw[2] = new float2(1, 1);
            verticesCcw[3] = new float2(0, 1);

            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);
            SplitToMonotone.Execute(m_Dcel, m_Dcel.GetInnerFace());

            // We check that each face is monotone.
            foreach (var face in m_Dcel.GetFacesIterator())
            {
                if (face.Index == m_Dcel.OuterFaceIndex)
                {
                    continue;
                }

                Assert.IsTrue(DoublyConnectedEdgeList.IsMonotone(face));
            }
        }

        [Test]
        [TestCaseSource(nameof(PolygonGenerationDataSource))]
        public void TriangulatePolygon(int numPoints, Rect bounds, float noise)
        {
            var verticesCcw = GeometryUtil.CreatePolygonCcw(Allocator.Temp, numPoints, bounds, noise, ref s_Random);
            m_Dcel.InitializeFromCcwVertices(verticesCcw, Allocator.Temp);
            Triangulate.Execute(m_Dcel);

            Assert.IsTrue(GeometryUtil.IsFullyTriangulated(m_Dcel));
        }

        static IEnumerable SimplifyDataSource
        {
            get
            {
                {
                    var input = new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                        Vector2.right,
                        Vector2.one,
                    }.ToList();

                    var output = new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                        Vector2.one,
                    }.ToList();

                    yield return new TestCaseData(input, output);
                }

                {
                    var input = new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                        Vector2.Lerp(Vector2.right, Vector2.one, .4f),
                        Vector2.one,
                    }.ToList();

                    var output = new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                        Vector2.one,
                    }.ToList();

                    yield return new TestCaseData(input, output);
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(SimplifyDataSource))]
        public void RemoveDuplicatesAndCollinearPoints(List<Vector2> input, List<Vector2> output)
        {
            Geometry.Utilities.RemoveDuplicatesAndCollinear(input, 1e-2f);

            Assert.IsTrue(input.Count == output.Count);
            for (var i = 0; i != input.Count; ++i)
            {
                var @in = input[i];
                var @out = output[i];
                Assert.IsTrue(Mathf.Approximately(@in.x, @out.x));
                Assert.IsTrue(Mathf.Approximately(@in.y, @out.y));
            }
        }
    }
}
