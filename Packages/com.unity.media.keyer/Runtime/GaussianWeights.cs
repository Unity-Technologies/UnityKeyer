using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
#if UNITY_EDITOR
using UnityEditor;

#endif
namespace Unity.Media.Keyer
{
    static class GaussianWeights
    {
        static readonly ComputeBufferLruCache<int> k_GaussianWeightsCache = new(12);

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        static void Initialize()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting -= Cleanup;
            EditorApplication.quitting += Cleanup;
#else
            Application.quitting -= Cleanup;
            Application.quitting += Cleanup;
#endif
        }

        static void Cleanup()
        {
            k_GaussianWeightsCache.Dispose();
        }

        public static ComputeBuffer Get(int weightCount)
        {
            Assert.IsTrue(weightCount > 0);

            ComputeBuffer gpuWeights;

            if (k_GaussianWeightsCache.TryGetValue(weightCount, out gpuWeights))
                return gpuWeights;

            var weights = new NativeArray<float>(weightCount, Allocator.Temp);
            Get(weights);

            gpuWeights = new ComputeBuffer(weights.Length, sizeof(float));
            gpuWeights.SetData(weights);
            k_GaussianWeightsCache.Store(weightCount, gpuWeights);

            return gpuWeights;
        }

        static void Get(NativeArray<float> data)
        {
            var integrationBound = 3f;
            var p = -integrationBound;
            var step = integrationBound * 2 / data.Length;
            for (var i = 0; i < data.Length; i++)
            {
                var w = Gaussian(p) * integrationBound * 2 / data.Length;
                data[i] = w;
                p += step;
            }
        }

        // Gaussian function, note that we assume standard-deviation=1.
        static float Gaussian(float x)
        {
            var a = 1.0f / math.sqrt(2 * Mathf.PI);
            var b = math.exp(-(x * x) / 2);
            return a * b;
        }
    }
}
