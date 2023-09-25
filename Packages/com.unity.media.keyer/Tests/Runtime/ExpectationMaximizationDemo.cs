using System;
using System.Collections;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer.Tests
{
    // This component allows users to iterate on Expectation Maximization clustering.
    // It is intended to be used by developers working on the algorithm.
    [ExecuteAlways]
    public class ExpectationMaximizationDemo : MonoBehaviour
    {
        static readonly Rect k_GuiRect = new(12, 0, 512, 512);

        struct Stats
        {
            public double ElapsedMs;
            public double GpuElapsedMs;
            public long BlockCount;
            public long GpuBlockCount;
        }

        [Serializable]
        struct Cluster
        {
            public Color Centroid;
            public float3x3 Covariance;
        }

        [Tooltip("Delay between the execution of convergence steps in seconds.")]
        [SerializeField, Range(.2f, 2)]
        float m_Delay;

        [Tooltip("If true, exports serialized data to be consumed by the python visualization.")]
        [SerializeField]
        bool m_NumpyExport;

        [Tooltip("Maximum number of voxels displayed in the python visualization.")]
        [SerializeField]
        int m_MaxVisualizationSamples;

        [Tooltip("If true, captures each convergence step using RenderDoc.")]
        [SerializeField]
        bool m_RenderDocCapture;

        [Tooltip("If true, resets cluster centroids procedurally before analysis.")]
        [SerializeField]
        bool m_ResetClustersProcedural;

        [Tooltip("The image to be analyzed.")]
        [SerializeField]
        Texture2D m_Source;

        [Tooltip("Number of clusters within the distribution.")]
        [SerializeField]
        int m_NumClusters;

        [Tooltip("Number of convergence steps.")]
        [SerializeField]
        int m_Iterations;

        [Tooltip("Clusters of the multivariate Gaussian distribution.")]
        [SerializeField]
        Cluster[] m_Clusters;

        ExpectationMaximization m_ExpectationMaximization = new();
        CommandBuffer m_CommandBuffer;
        CustomSampler m_Sampler;
        int m_CurrentStep;
        int m_ReceivedNumSamples;
        string m_OutputDirectory;
        Stats m_Stats;
        readonly StringBuilder m_StringBuilder = new();

        // Clunky, temporary.
        [SerializeField, HideInInspector]
        Color[] m_SavedCentroids;

        void OnGUI()
        {
            if (m_Source != null)
            {
                var screenRect = new Rect(0, 0, Screen.width, Screen.height);
                var imgAspect = m_Source.width / (float)m_Source.height;
                var imgRect = Fit(screenRect, imgAspect);
                GUI.DrawTexture(imgRect, m_Source);
            }

            GUI.color = Color.magenta;

            // Draw profiling stats.
            m_StringBuilder.Clear();
            m_StringBuilder.AppendLine("Profiling:");
            m_StringBuilder.AppendLine($"ElapsedMs: {m_Stats.ElapsedMs}");
            m_StringBuilder.AppendLine($"GpuElapsedMs: {m_Stats.GpuElapsedMs}");
            m_StringBuilder.AppendLine($"BlockCount: {m_Stats.BlockCount}");
            m_StringBuilder.AppendLine($"GpuBlockCount: {m_Stats.GpuBlockCount}");

            GUI.Label(k_GuiRect, m_StringBuilder.ToString());
        }

        void OnValidate()
        {
            m_Delay = Mathf.Max(.05f, m_Delay);
            m_Iterations = Mathf.Max(0, m_Iterations);
            m_NumClusters = Mathf.Clamp(m_NumClusters, 2, 16);
            m_MaxVisualizationSamples = Mathf.Clamp(m_MaxVisualizationSamples, 1024, 1024 * 8);

            AllocateClustersIfNeeded();

            // Save current centroids so that we can restore them later.
            if (m_SavedCentroids == null || m_SavedCentroids.Length != m_Clusters.Length)
            {
                m_SavedCentroids = new Color[m_Clusters.Length];
            }

            var index = 0;
            foreach (var cluster in m_Clusters)
            {
                m_SavedCentroids[index++] = cluster.Centroid;
            }
        }

        void OnEnable()
        {
            if (m_Sampler == null)
            {
                m_Sampler = CustomSampler.Create("Expectation Maximization", true);
            }

            m_CommandBuffer = new CommandBuffer();
            m_CommandBuffer.name = "ExpectationMaximization";
            m_ExpectationMaximization.Initialize(KeyerResources.GetInstance());
            m_ExpectationMaximization.Completed += OnComplete;
            m_ExpectationMaximization.SamplesReadback += OnSamplesReadback;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            m_ExpectationMaximization.SamplesReadback -= OnSamplesReadback;
            m_ExpectationMaximization.Completed -= OnComplete;
            m_ExpectationMaximization.Dispose();
            m_CommandBuffer.Dispose();
        }

        void Update()
        {
            // Fetch profiling info.
            // The Recorder has a three frame delay for GPU data so we poke it on each update.
            var recorder = m_Sampler.GetRecorder();
            if (recorder.isValid)
            {
                if (recorder.elapsedNanoseconds > 0)
                {
                    m_Stats.ElapsedMs = recorder.elapsedNanoseconds / 1e6;
                    m_Stats.BlockCount = recorder.sampleBlockCount;
                }

                if (recorder.gpuElapsedNanoseconds > 0)
                {
                    m_Stats.GpuElapsedMs = recorder.gpuElapsedNanoseconds / 1e6;
                    m_Stats.GpuBlockCount = recorder.gpuSampleBlockCount;
                }
            }
        }

        void AllocateClustersIfNeeded()
        {
            if (m_Clusters == null || m_Clusters.Length != m_NumClusters)
            {
                m_Clusters = new Cluster[m_NumClusters];
            }
        }

        [ContextMenu("Reset Clusters Saved")]
        void ResetClustersSaved()
        {
            if (m_Clusters != null && m_SavedCentroids != null)
            {
                // Best effort.
                var count = Mathf.Min(m_Clusters.Length, m_SavedCentroids.Length);
                for (var i = 0; i != count; ++i)
                {
                    m_Clusters[i].Centroid = m_SavedCentroids[i];
                }
            }
        }

        [ContextMenu("Reset Clusters Procedural")]
        void ResetClustersProcedural()
        {
            AllocateClustersIfNeeded();

            // Could be cached, doesn't matter much here.
            var initialCentroids = new NativeArray<Vector3>(m_Clusters.Length, Allocator.Temp);
            InitializeCentroids(initialCentroids, m_Clusters.Length);
            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                m_Clusters[i].Centroid = initialCentroids[i].ToColor();
                m_Clusters[i].Covariance = float3x3.identity;
            }
        }

        [ContextMenu("Execute")]
        void StartExecute()
        {
            StopAllCoroutines();

            if (m_Source == null)
            {
                throw new InvalidOperationException($"{nameof(m_Source)} must not be null.");
            }

            StartCoroutine(Execute());
        }

        IEnumerator Execute()
        {
            m_CurrentStep = 0;

            if (m_ResetClustersProcedural)
            {
                ResetClustersProcedural();
            }
            else
            {
                ResetClustersSaved();
            }

            m_ExpectationMaximization.ScheduleSamplesReadback = m_NumpyExport;

            // Prepare directory for output.
            if (m_NumpyExport)
            {
                m_OutputDirectory = Utilities.GetAbsoluteOutputDirectory("TestResults", m_Source.name);
                Utilities.CreateOrClearDirectory(m_OutputDirectory);
            }

            var initialCentroids = new NativeArray<float3>(m_Clusters.Length, Allocator.Temp);
            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                initialCentroids[i] = m_Clusters[i].Centroid.ToFloat3();
            }

            var iterator = m_ExpectationMaximization.ExecuteWithIterator(m_NumClusters, initialCentroids, m_Source, m_Iterations);
            while (iterator.Next(m_CommandBuffer))
            {
                using (new CustomSamplerScope(m_Sampler))
                {
                    if (m_RenderDocCapture)
                    {
                        using var captureScope = EditorBridge.CreateRenderDocCaptureScope();
                        Graphics.ExecuteCommandBuffer(m_CommandBuffer);
                    }
                    else
                    {
                        Graphics.ExecuteCommandBuffer(m_CommandBuffer);
                    }
                }

                m_CommandBuffer.Clear();

                yield return new WaitForSeconds(m_Delay);
            }

            // Write a .bat file to easily run the visualization.
            if (m_NumpyExport)
            {
                var numSamples = m_ReceivedNumSamples > m_MaxVisualizationSamples ? m_MaxVisualizationSamples : -1;

                // -1 accounts for the first step which is binning & sample processing.
                WriteVisualizationScript(m_OutputDirectory, m_CurrentStep - 1, numSamples);
            }
        }

        void OnComplete(ExpectationMaximization.ReadbackResult readbackResult)
        {
            Assert.IsTrue(readbackResult.Centroids.Length == m_NumClusters);
            Assert.IsTrue(readbackResult.Covariances.Length == m_NumClusters);

            AllocateClustersIfNeeded();

            for (var i = 0; i != m_NumClusters; ++i)
            {
                m_Clusters[i] = new Cluster
                {
                    Centroid = readbackResult.Centroids[i].ToColor(),
                    Covariance = readbackResult.Covariances[i]
                };
            }

            if (m_NumpyExport)
            {
                NumpyExport.Write(Path.Combine(m_OutputDirectory, $"centroids_{m_CurrentStep:D3}.txt"), readbackResult.Centroids);
                NumpyExport.Write(Path.Combine(m_OutputDirectory, $"covariances_{m_CurrentStep:D3}.txt"), readbackResult.Covariances);
            }

            ++m_CurrentStep;
        }

        void OnSamplesReadback(NativeArray<Vector4> samples, int size)
        {
            m_ReceivedNumSamples = size;

            if (m_NumpyExport)
            {
                NumpyExport.Write(Path.Combine(m_OutputDirectory, "samples.txt"), samples, size);
            }
        }

        static void WriteVisualizationScript(string directory, int steps, int numSamples = -1)
        {
            var scriptPath = Path.GetFullPath("Packages/com.unity.media.Keyer/Scripts/clustering_viz.py");
            var outputPath = Path.Combine(directory, "visualize.bat");

            using var writer = new StreamWriter(outputPath);
            writer.Write($"python {scriptPath} -d {directory} -s {steps}");
            if (numSamples != -1)
            {
                writer.Write($" -m {numSamples}");
            }
        }

        static void InitializeCentroids(NativeArray<Vector3> centroids, int count)
        {
            for (var i = 0; i != count; ++i)
            {
                centroids[i] = Color.HSVToRGB(i / (float)count, .5f, .5f).ToVector3();
            }
        }

        static Rect Fit(Rect rect, float aspect)
        {
            var rectAspect = rect.width / rect.height;
            return rectAspect > aspect ? new Rect(rect.x, rect.y, aspect * rect.height, rect.height) : new Rect(rect.x, rect.y, rect.width, rect.width / aspect);
        }
    }
}
