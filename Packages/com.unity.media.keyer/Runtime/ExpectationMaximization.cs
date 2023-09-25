using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    class ExpectationMaximization : IDisposable
    {
        public struct ReadbackResult : IDisposable
        {
            public NativeArray<float3> Centroids;
            public NativeArray<float3x3> Covariances;

            bool m_AssignedCentroids;
            bool m_AssignedCovariances;

            public void AllocateIfNeeded(int numClusters)
            {
                Utilities.AllocateNativeArrayIfNeeded(ref Centroids, numClusters);
                Utilities.AllocateNativeArrayIfNeeded(ref Covariances, numClusters);
            }

            public void Reset()
            {
                m_AssignedCentroids = false;
                m_AssignedCovariances = false;
            }

            public bool IsReady() => m_AssignedCentroids && m_AssignedCovariances;

            public void MarkAssignedCentroids() => m_AssignedCentroids = true;
            public void MarkAssignedCovariances() => m_AssignedCovariances = true;

            public void Dispose()
            {
                Utilities.DeallocateNativeArrayIfNeeded(ref Centroids);
                Utilities.DeallocateNativeArrayIfNeeded(ref Covariances);
            }
        }

        // No need for the complete IEnumerator pattern.
        // Iterator should not be instantiated outside of this class,
        // but all this is internal and it's reasonable to expect proper use here.
        public struct Iterator
        {
            ExpectationMaximization m_Instance;
            Texture m_Source;
            int m_Iteration;
            int m_TotalIterations;
            bool m_ContinuousCapture;
            bool m_Initialized;

            public Iterator(ExpectationMaximization instance, Texture source, int totalIterations, bool continuousCapture)
            {
                m_Iteration = 0;
                m_Initialized = false;
                m_Instance = instance;
                m_Source = source;
                m_TotalIterations = totalIterations;
                m_ContinuousCapture = continuousCapture;
            }

            public bool Next(CommandBuffer cmd)
            {
                if (!m_Initialized)
                {
                    // By this time centroids should have been initialized.
                    m_Instance.InitializationStep(cmd, m_Source);

                    // We want to visualize the initial data.
                    if (m_ContinuousCapture)
                    {
                        m_Instance.ScheduleCentroidAndCovarianceReadback(cmd);
                    }

                    m_Initialized = true;
                    return true;
                }
                else
                {
                    if (m_Iteration < m_TotalIterations)
                    {
                        ++m_Iteration;

                        m_Instance.ConvergenceStep(cmd);

                        if (m_ContinuousCapture || m_Iteration == m_TotalIterations)
                        {
                            m_Instance.ScheduleCentroidAndCovarianceReadback(cmd);
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        const int k_GroupSize = 32;
        const int k_GridSize = 32;
        const int k_VoxelCount = k_GridSize * k_GridSize * k_GridSize;
        const int k_MaxRequiredReductionSteps = 3;

        readonly DoubleBuffer<float> m_SumBuffer = new();
        readonly DoubleBuffer<float3> m_CentroidBuffer = new();
        readonly DoubleBuffer<float3> m_CovarianceBuffer = new();
        ComputeBuffer m_InverseCovariancesBuffer;
        ComputeBuffer m_SqrtDetReciprocalsBuffer;
        ComputeBuffer m_WeightBuffer;
        ComputeBuffer m_ColorBinBuffer;
        ComputeBuffer m_SelectedColorBinBuffer;
        ComputeBuffer m_IndirectBuffer;
        ComputeBuffer m_SamplingCoordsBuffer;
        ComputeShader m_Shader;
        KernelIds.ExpectationMaximizationIds m_KernelIds;
        ReadbackResult m_ReadbackResult;
        bool m_ScheduleSamplesReadback;
        int m_NumClusters;
        int m_NumSamples;

        public event Action<ReadbackResult> Completed = delegate { };
        public event Action<NativeArray<Vector4>, int> SamplesReadback = delegate { };

        public bool ScheduleSamplesReadback
        {
            set => m_ScheduleSamplesReadback = value;
        }

        public void Initialize(KeyerResources resources)
        {
            m_KernelIds = resources.KernelIds.ExpectationMaximization;
            m_Shader = resources.Shaders.ExpectationMaximization;
        }

        public void Dispose()
        {
            m_ReadbackResult.Dispose();
            m_SumBuffer.Dispose();
            m_CentroidBuffer.Dispose();
            m_CovarianceBuffer.Dispose();
            Utilities.DeallocateIfNeeded(ref m_InverseCovariancesBuffer);
            Utilities.DeallocateIfNeeded(ref m_SqrtDetReciprocalsBuffer);
            Utilities.DeallocateIfNeeded(ref m_WeightBuffer);
            Utilities.DeallocateIfNeeded(ref m_ColorBinBuffer);
            Utilities.DeallocateIfNeeded(ref m_SelectedColorBinBuffer);
            Utilities.DeallocateIfNeeded(ref m_IndirectBuffer);
            Utilities.DeallocateIfNeeded(ref m_SamplingCoordsBuffer);
        }

        // The iterator based execution is useful when working on or troubleshooting the component.
        public Iterator ExecuteWithIterator(
            int numClusters,
            NativeArray<float3> initialCentroids,
            Texture source,
            int iterations)
        {
            PreInitialization(numClusters, initialCentroids, source);

            // In this code path we will readback all intermediary results.
            return new Iterator(this, source, iterations, true);
        }

        public void Execute(
            CommandBuffer cmd,
            int numClusters,
            NativeArray<float3> initialCentroids,
            Texture source,
            int iterations)
        {
            PreInitialization(numClusters, initialCentroids, source);

            var iterator = new Iterator(this, source, iterations, false);
            while (iterator.Next(cmd)) { }
        }

        void PreInitialization(int numClusters, NativeArray<float3> initialCentroids, Texture source)
        {
            m_NumClusters = numClusters;
            m_NumSamples = source.width * source.height;
            var numWeights = k_VoxelCount * m_NumClusters;

            Assert.IsTrue(initialCentroids.Length >= m_NumClusters);

            m_CentroidBuffer.AllocateIfNeeded(numWeights);
            m_CentroidBuffer.In.SetData(initialCentroids);

            // Doing this here allows us to do it once.
            m_ReadbackResult.AllocateIfNeeded(m_NumClusters);
            m_ReadbackResult.Reset();
        }

        void InitializationStep(CommandBuffer cmd, Texture source)
        {
            var numWeights = k_VoxelCount * m_NumClusters;

            cmd.SetComputeIntParam(m_Shader, ShaderIDs._NumClusters, m_NumClusters);

            // Reset color bins. One thread per voxel.
            {
                Utilities.AllocateBufferIfNeeded<uint>(ref m_ColorBinBuffer, k_VoxelCount);

                var kernel = m_KernelIds.ResetColorBins;
                var shader = m_Shader;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._ColorBins, m_ColorBinBuffer);
                cmd.DispatchCompute(shader, kernel, k_VoxelCount / k_GroupSize, 1, 1);
            }

            // Update color bins. One thread per sample.
            {
                var kernel = m_KernelIds.UpdateColorBins;
                var shader = m_Shader;

                cmd.SetComputeVectorParam(shader, ShaderIDs._SourceSize, new Vector2(source.width, source.height));
                cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._SourceTexture, source);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._ColorBins, m_ColorBinBuffer);
                var warpX = Mathf.CeilToInt((float)source.width / k_GroupSize);
                var warpY = Mathf.CeilToInt((float)source.height / k_GroupSize);
                cmd.DispatchCompute(shader, kernel, warpX, warpY, 1);

                if (m_ScheduleSamplesReadback)
                {
                    cmd.RequestAsyncReadback(m_ColorBinBuffer, m_ColorBinBuffer.stride * k_VoxelCount, 0, OnColorBinsBufferReadback);
                }
            }

            // Filter populated color bins
            {
                // Actually uint2 but same byte count.
                if (!Utilities.AllocateBufferIfNeeded<Vector2Int>(ref m_SelectedColorBinBuffer, k_VoxelCount, ComputeBufferType.Append))
                {
                    var kernel = m_KernelIds.ResetSelectedColorBins;
                    var shader = m_Shader;

                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SelectedColorBins, m_SelectedColorBinBuffer);
                    cmd.DispatchCompute(shader, kernel, k_VoxelCount / k_GroupSize, 1, 1);
                    cmd.SetBufferCounterValue(m_SelectedColorBinBuffer, 0);
                }

                {
                    Utilities.AllocateBufferIfNeeded<uint>(ref m_IndirectBuffer, 4 * 4, ComputeBufferType.IndirectArguments);

                    var kernel = m_KernelIds.SelectColorBins;
                    var shader = m_Shader;

                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._ColorBins, m_ColorBinBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._AppendSelectedColorBins, m_SelectedColorBinBuffer);
                    cmd.DispatchCompute(shader, kernel, k_VoxelCount / k_GroupSize, 1, 1);

                    // Copy the count of populated color bins to indirect arguments buffer.
                    // We will then infer arguments for subsequent reductions.
                    cmd.CopyCounterValue(m_SelectedColorBinBuffer, m_IndirectBuffer, 0);
                }
            }

            // Update Indirect Arguments.
            {
                var kernel = m_KernelIds.UpdateIndirectArguments;
                var shader = m_Shader;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._IndirectBuffer, m_IndirectBuffer);
                cmd.DispatchCompute(shader, kernel, 1, 1, 1);
            }

            // Reset covariance.
            {
                // Note the * 2, We encode symmetric matrices using 2 float3.
                m_CovarianceBuffer.AllocateIfNeeded(numWeights * 2);

                var kernel = m_KernelIds.InitializeCovariances;
                var shader = m_Shader;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesIn, m_CovarianceBuffer.In);

                // We only want to process m_NumClusters items.
                var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
                cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
            }
        }

        // Performs one iteration of the EM algorithm.
        void ConvergenceStep(CommandBuffer cmd)
        {
            var numWeights = k_VoxelCount * m_NumClusters;

            cmd.SetComputeIntParam(m_Shader, ShaderIDs._NumClusters, m_NumClusters);

            // Precalculate data required to evaluate gaussian distribution.
            {
                Utilities.AllocateBufferIfNeeded<float>(ref m_SqrtDetReciprocalsBuffer, PadToFitGroupSize(m_NumClusters));

                // Note the * 2, We encode symmetric matrices using 2 float3.
                Utilities.AllocateBufferIfNeeded<Vector3>(ref m_InverseCovariancesBuffer, PadToFitGroupSize(m_NumClusters * 2));

                var kernel = m_KernelIds.ProcessCovariances;
                var shader = m_Shader;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SqrtDetReciprocals, m_SqrtDetReciprocalsBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._InverseCovariances, m_InverseCovariancesBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesIn, m_CovarianceBuffer.In);

                // We only want to process m_NumClusters items.
                var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
                cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
            }

            // Update weights and centroids. One thread per voxel.
            {
                Utilities.AllocateBufferIfNeeded<Vector3>(ref m_WeightBuffer, numWeights);

                // TODO Optimization can allocate less memory for the Out buffer.
                // TODO Is there a way to apply normalization earlier?
                m_SumBuffer.AllocateIfNeeded(numWeights);

                var shader = m_Shader;
                var kernel = m_KernelIds.UpdateWeightsAndCentroids;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._Weights, m_WeightBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsIn, m_CentroidBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsOut, m_CentroidBuffer.Out);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SqrtDetReciprocals, m_SqrtDetReciprocalsBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._InverseCovariances, m_InverseCovariancesBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SelectedColorBins, m_SelectedColorBinBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SumsOut, m_SumBuffer.Out);
                cmd.DispatchCompute(shader, kernel, m_IndirectBuffer, 0);
            }

            // Reduce sums and centroids. Each thread processes all clusters.
            {
                var shader = m_Shader;
                var kernel = m_KernelIds.ReduceSumsAndCentroids;

                for (var i = 0; i != k_MaxRequiredReductionSteps; ++i)
                {
                    m_SumBuffer.Swap();
                    m_CentroidBuffer.Swap();
                    cmd.SetComputeIntParam(shader, ShaderIDs._IndirectArgsOffset, 4 * i);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._IndirectBuffer, m_IndirectBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SumsIn, m_SumBuffer.In);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SumsOut, m_SumBuffer.Out);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsIn, m_CentroidBuffer.In);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsOut, m_CentroidBuffer.Out);
                    cmd.DispatchCompute(shader, kernel, m_IndirectBuffer, 4u * (uint)i * sizeof(uint));
                }

                m_SumBuffer.Swap();
                m_CentroidBuffer.Swap();
            }

            // Update covariance. One thread per voxel.
            {
                var shader = m_Shader;
                var kernel = m_KernelIds.UpdateCovariances;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SelectedColorBins, m_SelectedColorBinBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsIn, m_CentroidBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SumsIn, m_SumBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._Weights, m_WeightBuffer);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesOut, m_CovarianceBuffer.Out);
                cmd.DispatchCompute(shader, kernel, m_IndirectBuffer, 0);
            }

            // Reduce covariance. Each thread processes all clusters.
            {
                var shader = m_Shader;
                var kernel = m_KernelIds.ReduceCovariances;

                for (var i = 0; i != k_MaxRequiredReductionSteps; ++i)
                {
                    m_CovarianceBuffer.Swap();
                    cmd.SetComputeIntParam(shader, ShaderIDs._IndirectArgsOffset, 4 * i);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._IndirectBuffer, m_IndirectBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesIn, m_CovarianceBuffer.In);
                    cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesOut, m_CovarianceBuffer.Out);
                    cmd.DispatchCompute(shader, kernel, m_IndirectBuffer, 4u * (uint)i * sizeof(uint));
                }

                m_CovarianceBuffer.Swap();
            }

            // Normalize centroids and covariances. One thread per cluster.
            {
                // Here we process the In buffers in place.
                var shader = m_Shader;
                var kernel = m_KernelIds.NormalizeCentroidsAndCovariances;

                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CovariancesIn, m_CovarianceBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._CentroidsIn, m_CentroidBuffer.In);
                cmd.SetComputeBufferParam(shader, kernel, ShaderIDs._SumsIn, m_SumBuffer.In);

                // We only want to process m_NumClusters items.
                var numGroups = Mathf.CeilToInt(m_NumClusters / (float)k_GroupSize);
                cmd.DispatchCompute(shader, kernel, numGroups, 1, 1);
            }
        }

        void ScheduleCentroidAndCovarianceReadback(CommandBuffer cmd)
        {
            // Swaps are done by this point.
            cmd.RequestAsyncReadback(m_CentroidBuffer.In, m_CentroidBuffer.In.stride * m_NumClusters, 0, OnCentroidBufferReadback);

            // Note the * 2, We encode symmetric matrices using 2 float3.
            cmd.RequestAsyncReadback(m_CovarianceBuffer.In, m_CovarianceBuffer.In.stride * 2 * m_NumClusters, 0, OnCovarianceBufferReadback);
        }

        void OnColorBinsBufferReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException(
                    $"{nameof(AsyncGPUReadbackRequest)} failed, \"{req.ToString()}\"");
            }

            var colorBins = req.GetData<uint>();
            Assert.IsTrue(colorBins.Length == k_VoxelCount);

            // We don't want to introduce a dependency on Unity.Collections.
            var data = new NativeArray<Vector4>(k_VoxelCount, Allocator.Temp);
            var index = 0;
            var total = 0u;
            for (var i = 0; i != k_VoxelCount; ++i)
            {
                var bin = colorBins[i];
                if (bin == 0)
                {
                    continue;
                }

                total += bin;
                var center = GetVoxelCenter(i);
                data[index++] = new Vector4(center.x, center.y, center.z, bin * k_VoxelCount / (float)m_NumSamples);
            }

            // Check that bins contains all input samples.
            Assert.IsTrue(m_NumSamples == total);
            SamplesReadback.Invoke(data, index);
        }

        void OnCentroidBufferReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException(
                    $"{nameof(AsyncGPUReadbackRequest)} failed, \"{req.ToString()}\"");
            }

            var centroids = req.GetData<float3>();
            Assert.IsTrue(centroids.Length == m_NumClusters);
            m_ReadbackResult.Centroids.CopyFrom(centroids);
            m_ReadbackResult.MarkAssignedCentroids();
            DispatchResultIfReady();
        }

        void OnCovarianceBufferReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException(
                    $"{nameof(AsyncGPUReadbackRequest)} failed, \"{req.ToString()}\"");
            }

            var covariances = req.GetData<Vector3>();
            Assert.IsTrue(covariances.Length == m_NumClusters * 2);

            // We encode symmetric matrices using 2 float3.
            for (var i = 0; i != m_NumClusters; ++i)
            {
                // See ReadMatrixSymmetric3x3 in CommonCompute.hlsl
                var r0 = covariances[i * 2];
                var r1 = covariances[i * 2 + 1];
                var m = new float3x3();
                m.c0 = r0;
                m.c1 = new float3(r0.y, r1.x, r1.y);
                m.c2 = new float3(r0.z, r1.y, r1.z);
                m_ReadbackResult.Covariances[i] = m;
            }

            m_ReadbackResult.MarkAssignedCovariances();
            DispatchResultIfReady();
        }

        void DispatchResultIfReady()
        {
            if (m_ReadbackResult.IsReady())
            {
                Completed.Invoke(m_ReadbackResult);
                m_ReadbackResult.Reset();
            }
        }

        // We prefer padding to index checks in each kernel execution.
        static int PadToFitGroupSize(int size)
        {
            return Mathf.CeilToInt(size / (float)k_GroupSize) * k_GroupSize;
        }

        static Vector3Int To3dIndex(int id, int dimension)
        {
            var z = id / (dimension * dimension);
            id -= z * dimension * dimension;

            var y = id / dimension;
            id -= y * dimension;

            var x = id / 1;
            return new Vector3Int(x, y, z);
        }

        static Vector3 GetVoxelCenter(int index)
        {
            var index3d = To3dIndex(index, k_GridSize);
            var center = new Vector3(index3d.x, index3d.y, index3d.z);
            center += Vector3.one * .5f;
            center.x /= k_GridSize;
            center.y /= k_GridSize;
            center.z /= k_GridSize;
            return center;
        }
    }
}
