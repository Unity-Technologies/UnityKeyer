using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Random = Unity.Mathematics.Random;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    [UnityPlatform(exclude = new[] { RuntimePlatform.WebGLPlayer })]
    class ClusteringTests
    {
        const string k_InputPath = "Packages/com.unity.media.keyer/Tests/Editor/ClusteringData/ClusteringTestData.asset";

        GaussianDistribution m_Input;
        GaussianDistribution.Cluster m_Reference;
        ExpectationMaximization m_Analyzer;
        CommandBuffer m_CommandBuffer;
        bool m_WaitingForReadback;

        static bool s_NotifiedIfUnsupported;

        static bool IsSupported()
        {
            if (SystemInfo.supportsComputeShaders && SystemInfo.supportsAsyncGPUReadback && IsComputeTestSupportedOnCurrentPlatform())
            {
                return true;
            }

            if (!s_NotifiedIfUnsupported)
            {
                Assert.Ignore($"ComputeShaders and/or AsyncGPUReadback not supported, Ignored {nameof(ClusteringTests)}.");
                s_NotifiedIfUnsupported = true;
            }

            return false;
        }

        // Borrowed from Tests/Unity.GraphicsTestsRunner/GfxTestProjectFolder/Assets/819-GraphicsBufferTests/GraphicsBufferTests.cs
        static bool IsComputeTestSupportedOnCurrentPlatform()
        {
            // For some reason, doing the compute test with async GPU readback
            // causes a test crash on some Katana platforms. So skip compute for those for now.
            switch (Application.platform)
            {
                case RuntimePlatform.WSAPlayerARM:
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerX86:
                    return false;
            }

            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D12:
                    return false;
            }

            return true;
        }

        [SetUp]
        public void Setup()
        {
            if (!IsSupported())
            {
                return;
            }

            m_Input = AssetDatabase.LoadAssetAtPath<GaussianDistribution>(k_InputPath);
            m_CommandBuffer = new CommandBuffer();
            m_CommandBuffer.name = "Clustering Test";
            m_Analyzer = new ExpectationMaximization();
            m_Analyzer.Initialize(KeyerResources.GetInstance());
            m_Analyzer.Completed += OnComplete;
        }

        [TearDown]
        public void TearDown()
        {
            if (!IsSupported())
            {
                return;
            }

            m_Analyzer.Completed -= OnComplete;
            m_Analyzer.Dispose();
            m_CommandBuffer.Dispose();
            m_Reference = default;
        }

        [Ignore("This is a mock test.")]
        [UnityTest]
        public IEnumerator ReconstructClustersProperly()
        {
            if (!IsSupported())
            {
                yield break;
            }

            var rand = Random.CreateFromIndex(0);

            // Make sure the test does not pass simply in absence of relevant input.
            Assert.IsTrue(m_Input.Clusters.Count > 0);

            foreach (var cluster in m_Input.Clusters)
            {
                // Track so that we can compare on readback.
                m_Reference = cluster;

                Assert.IsTrue(cluster.Centroids.Length == cluster.Covariances.Length);
                var numClusters = cluster.Centroids.Length;
                var initialCentroids = new NativeArray<float3>(numClusters, Allocator.Temp);
                for (var i = 0; i != numClusters; ++i)
                {
                    initialCentroids[i] = rand.NextFloat3();
                }

                m_Analyzer.Execute(m_CommandBuffer, numClusters, initialCentroids, cluster.Image, 48);

                m_WaitingForReadback = true;
                var watchDog = 0;

                Graphics.ExecuteCommandBuffer(m_CommandBuffer);

                m_CommandBuffer.Clear();

                // Wait for result readback and comparison.
                while (m_WaitingForReadback)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;

                    if (++watchDog > 8)
                    {
                        throw new InvalidOperationException("Readback timed out.");
                    }
                }
            }
        }

        void OnComplete(ExpectationMaximization.ReadbackResult result)
        {
            var numClusters = m_Reference.Centroids.Length;

            Assert.IsTrue(m_Reference.Covariances.Length == numClusters);
            Assert.IsTrue(result.Centroids.Length == numClusters);
            Assert.IsTrue(result.Covariances.Length == numClusters);

            // No guarantee on the order of clusters in result so we must match with reference clusters order.
            var resIndices = new int[numClusters];
            GetMatchingOrder(m_Reference.Centroids, result.Centroids, resIndices);

            // The thresholds for comparison are a little arbitrary.
            // The one thing to keep in mind is that the procedural clusters used
            // should not be too large nor too close to the RGB cube edges.
            // Otherwise some of the samples generated get clamped and it skews the evaluated result.
            for (var i = 0; i != numClusters; ++i)
            {
                var centroidDistSq = math.distancesq(m_Reference.Centroids[i], result.Centroids[resIndices[i]]);
                Assert.IsTrue(centroidDistSq < 2e-4f);

                var correlationMatrixDistance = AlgebraUtil.CorrelationMatrixDistance(m_Reference.Covariances[i], result.Covariances[resIndices[i]]);
                Assert.IsTrue(correlationMatrixDistance < 1e-4f);
            }

            // Allow the testing routine to resume.
            m_WaitingForReadback = false;
        }

        static readonly HashSet<int> s_IndicesSet = new();

        // For each refArr value, find the index of the closest arr value.
        // Note: while refArr and arr having different types is not elegant it's better
        // then paying the price of a conversion.
        static void GetMatchingOrder(float3[] refArr, NativeArray<float3> arr, int[] indices)
        {
            var len = refArr.Length;
            Assert.IsTrue(arr.Length == len);
            Assert.IsTrue(indices.Length == len);

            s_IndicesSet.Clear();
            for (var i = 0; i != len; ++i)
            {
                s_IndicesSet.Add(i);
            }

            for (var i = 0; i != len; ++i)
            {
                var refValue = refArr[i];
                var closestIndex = 0;
                var minDistSq = float.MaxValue;
                foreach (var index in s_IndicesSet)
                {
                    var distSq = math.distancesq(refValue, arr[index]);
                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        closestIndex = index;
                    }
                }

                s_IndicesSet.Remove(closestIndex);
                indices[i] = closestIndex;
            }
        }
    }
}
