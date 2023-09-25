using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    [Serializable]
    class KeyingColorAnalyzer
    {
        public enum BatchCount : short
        {
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
        }

        struct Settings
        {
            public Color ReferenceKeyingColor;
            public bool ApplyGammaToSource;
            public int BatchSize;
            public BatchCount BatchCount;
        }

        const int k_MinBatchSize = 2;
        const int k_MaxBatchSize = 32;
        const int k_FilterGroupSize = 32;
        const int k_ReduceGroupSize = 64;

        LocalKeyword m_ApplyGammaToSourceKeyword;
        ComputeBuffer m_ColorBufferA;
        ComputeBuffer m_ColorBufferB;
        ComputeBuffer m_SampleCoords;
        ComputeShader m_Shader;
        KernelIds.KeyExtractIds m_KernelIds;

        public event Action<Color> Completed = delegate { };

        public void Initialize(KeyerResources resources)
        {
            m_Shader = resources.Shaders.KeyExtract;
            m_KernelIds = resources.KernelIds.KeyExtract;
            m_ApplyGammaToSourceKeyword = new LocalKeyword(m_Shader, "APPLY_GAMMA_TO_SOURCE");
        }

        public void Dispose()
        {
            Utilities.DeallocateIfNeeded(ref m_ColorBufferA);
            Utilities.DeallocateIfNeeded(ref m_ColorBufferB);
            Utilities.DeallocateIfNeeded(ref m_SampleCoords);
        }

        public void Execute(CommandBuffer cmd, Texture source, Color referenceKeyingColor, bool applyGammaToSource, BatchCount batchCount = BatchCount._64, int batchSize = 4)
        {
            Execute(cmd, new Settings
            {
                ReferenceKeyingColor = referenceKeyingColor,
                ApplyGammaToSource = applyGammaToSource,
                BatchSize = Mathf.Clamp(batchSize, k_MinBatchSize, k_MaxBatchSize),
                BatchCount = batchCount,
            }, source);
        }

        void Execute(CommandBuffer cmd, Settings settings, Texture source)
        {
            // Handle whether or not the source requires gamma transformation.
            if (m_Shader.IsKeywordEnabled(m_ApplyGammaToSourceKeyword) != settings.ApplyGammaToSource)
            {
                if (settings.ApplyGammaToSource)
                {
                    m_Shader.EnableKeyword(m_ApplyGammaToSourceKeyword);
                }
                else
                {
                    m_Shader.DisableKeyword(m_ApplyGammaToSourceKeyword);
                }
            }

            Filter(cmd, settings.ReferenceKeyingColor, source, settings);
            Reduce(cmd);
        }

        void Filter(CommandBuffer cmd, Color referenceKeyingColor, Texture source, Settings settings)
        {
            var batchSize = settings.BatchSize;
            var batchCount = (int)settings.BatchCount;
            var totalSamples = batchCount * batchSize;

            Utilities.AllocateBufferIfNeeded<Vector3>(ref m_ColorBufferA, batchCount);
            Utilities.AllocateBufferIfNeeded<Vector2>(ref m_SampleCoords, totalSamples);

            // TODO regen if needed only
            var rnd = new NativeArray<Vector2>(totalSamples, Allocator.Temp);
            Utilities.GetLowDiscrepancySequence(rnd);
            m_SampleCoords.SetData(rnd);

            var shader = m_Shader;
            var kernelId = m_KernelIds.Filter;

            // Bind uniforms not tied to a specific kernel.
            cmd.SetComputeVectorParam(shader, ShaderIDs._KeyingColorRGB, referenceKeyingColor);
            cmd.SetComputeIntParam(shader, ShaderIDs._BatchSize, batchSize);

            // Bind uniforms for the KeyExtractFilter kernel
            cmd.SetComputeTextureParam(shader, kernelId, ShaderIDs._SourceTexture, source);

            // Replicating Legacy handling of texture parameters.
            cmd.SetComputeVectorParam(shader, ShaderIDs._SourceTexelSize,
                new Vector4(source.width, source.height, 1 / (float)source.width, 1 / (float)source.height));

            cmd.SetComputeBufferParam(shader, kernelId, ShaderIDs._SampleCoords, m_SampleCoords);
            cmd.SetComputeBufferParam(shader, kernelId, ShaderIDs._Colors, m_ColorBufferA);

            // We use a [minBatchCount,1,1] Thread Group Size.
            // IMPORTANT: Must divide by lowest possible number of batch count, the amount it reduces by.
            var batchGroups = batchCount / k_FilterGroupSize;
            cmd.DispatchCompute(shader, kernelId, batchGroups, 1, 1);
        }

        void Reduce(CommandBuffer cmd)
        {
            var shader = m_Shader;
            var kernelId = m_KernelIds.Reduce;
            var count = m_ColorBufferA.count;
            var reduceGroups = Mathf.CeilToInt(count / (float)k_ReduceGroupSize);

            Utilities.AllocateBufferIfNeeded<Vector3>(ref m_ColorBufferB, reduceGroups);

            var colorsIn = m_ColorBufferA;
            var colorsOut = m_ColorBufferB;

            for (; ; )
            {
                cmd.SetComputeIntParam(shader, ShaderIDs._Count, count);
                cmd.SetComputeBufferParam(shader, kernelId, ShaderIDs._ColorsIn, colorsIn);
                cmd.SetComputeBufferParam(shader, kernelId, ShaderIDs._ColorsOut, colorsOut);
                cmd.DispatchCompute(shader, kernelId, reduceGroups, 1, 1);

                // Reduction complete.
                if (reduceGroups == 1)
                {
                    break;
                }

                // Each group writes one entry to the destination buffer.
                count = reduceGroups;
                reduceGroups = Mathf.CeilToInt(reduceGroups / (float)k_ReduceGroupSize);
                Utilities.Swap(ref colorsIn, ref colorsOut);
            }

            // We ask for readback of the 1st value in the buffer which is the final result of the averaging process.
            cmd.RequestAsyncReadback(colorsOut, colorsOut.stride, 0, OnBufferReadback);
        }

        void OnBufferReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException(
                    $"{nameof(AsyncGPUReadbackRequest)} failed, \"{req.ToString()}\"");
            }

            var colors = req.GetData<Vector3>();
            Assert.IsTrue(colors.Length == 1);
            var color = colors[0];
            Completed.Invoke(new Color(color.x, color.y, color.z));
        }
    }
}
