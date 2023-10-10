using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Random = Unity.Mathematics.Random;

namespace Unity.Media.Keyer.Tests
{
    // Draws random samples from a multivariate Gaussian distribution.
    static class GaussianSampler
    {
        [BurstCompile]
        struct GenerateNormalJob : IJobParallelFor
        {
            [NativeSetThreadIndex]
            public int ThreadIndex;
            [NativeDisableParallelForRestriction]
            public NativeArray<Random> RandomGenerators;
            [NativeDisableParallelForRestriction]
            public NativeArray<float3> Samples;

            // Note that we generate 2 samples at a time since Box-Muller works on pairs.
            public void Execute(int index)
            {
                var rnd = RandomGenerators[ThreadIndex];
                var r0 = float3.zero;
                var r1 = float3.zero;

                for (var i = 0; i != 3; ++i)
                {
                    // First draw 2 samples from uniform [0, 1] distribution.
                    var u1 = rnd.NextFloat();
                    var u2 = rnd.NextFloat();

                    // The generate 2 normal samples using Box-Muller transform.
                    var a = math.sqrt(-2 * math.log(u1));
                    var b = 2 * math.PI * u2;
                    r0[i] = a * math.sin(b);
                    r1[i] = a * math.cos(b);
                }

                Samples[index * 2] = r0;
                Samples[index * 2 + 1] = r1;

                // Don't forget to write back the generator, its state changed.
                RandomGenerators[ThreadIndex] = rnd;
            }
        }

        [BurstCompile]
        struct GenerateMultiVariateNormal : IJobParallelFor
        {
            public float3 Centroid;
            public float3x3 CholeskyMatrix;
            public int StartIndex;
            [ReadOnly]
            public NativeArray<float3> Input;
            [NativeDisableParallelForRestriction]
            public NativeArray<Color32> Output;

            /*
            We generate samples from clusters using the formula:
            x = m + L.u
               where
                   m: centroid
                   L: Cholesky decomposition of covariance
                   u: random samples from a normal (Gaussian) distribution
            */
            public void Execute(int index)
            {
                var s = math.saturate(Centroid + math.mul(CholeskyMatrix, Input[index]));
                Output[StartIndex + index] = new Color32(
                    (byte)(s.x * 255), (byte)(s.y * 255), (byte)(s.z * 255), 255);
            }
        }

        public const int BlockSize = 32;

        // Draw samples from a multivariate Gaussian distribution.
        // The distribution is described by its centroid and covariance matrix.
        // Indices must be multiples of the block size.
        public static void DrawSamples(
            float3 centroid, float3x3 covariance,
            NativeArray<Color32> samples, int startIndex = 0, int numSamples = -1)
        {
            var length = numSamples == -1 ? samples.Length : numSamples;
            Assert.IsTrue(length % BlockSize == 0);

            // Generate one random generator per thread.
            var random = Random.CreateFromIndex(0);
            var randomGenerators = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.TempJob);
            for (var i = 0; i < randomGenerators.Length; i++)
            {
                randomGenerators[i] = new Random((uint)random.NextInt());
            }

            var normalSamples = new NativeArray<float3>(length, Allocator.TempJob);

            var normalJob = new GenerateNormalJob
            {
                RandomGenerators = randomGenerators,
                Samples = normalSamples
            };

            var multiVariateJob = new GenerateMultiVariateNormal
            {
                Centroid = centroid,
                CholeskyMatrix = AlgebraUtil.CholeskyDecomposition(covariance),
                StartIndex = startIndex,
                Input = normalSamples,
                Output = samples
            };

            var normalHandle = normalJob.Schedule(length / 2, BlockSize);
            var multiVariateHandle = multiVariateJob.Schedule(length, BlockSize, normalHandle);
            multiVariateHandle.Complete();

            randomGenerators.Dispose();
            normalSamples.Dispose();
        }
    }
}
