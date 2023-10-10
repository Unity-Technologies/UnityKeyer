using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering.HighDefinition;

namespace Unity.Media.Keyer.Samples.ExtractKeyingColorDemo
{
    [ExecuteAlways]
    public class ExtractKeyingColorDemo : MonoBehaviour
    {
        [Serializable]
        struct Settings
        {
            public Color ReferenceKeyingColor;
            public bool ApplyGammaToSource;
            [Range(2, 32)]
            public int BatchSize;
            public KeyingColorAnalyzer.BatchCount BatchCount;
        }

        static readonly KeyingColorAnalyzer.BatchCount[] k_BatchCounts =
        {
            KeyingColorAnalyzer.BatchCount._32,
            KeyingColorAnalyzer.BatchCount._64,
            KeyingColorAnalyzer.BatchCount._128,
            KeyingColorAnalyzer.BatchCount._256,
            KeyingColorAnalyzer.BatchCount._512
        };

        struct Sample
        {
            public KeyingColorAnalyzer.BatchCount BatchCount;
            public int BatchSize;
            public Color KeyingColor;
        }

        [SerializeField]
        ConfigureNoiseMaterial m_ConfigureNoiseMaterial;

        [SerializeField]
        Settings m_Settings;

        [SerializeField]
        Color m_ReferenceKeyingColor = Color.green;

        [SerializeField]
        Vector3 m_ReferenceKeyingColorBias;

        [SerializeField]
        Color m_EstimatedKeyingColor;

        RenderTexture m_Capture;
        bool m_KeyingColorChanged;

        readonly KeyingColorAnalyzer m_Analyzer = new();
        readonly List<Sample> m_Samples = new();
        readonly StringBuilder m_StringBuilder = new();

        void OnEnable()
        {
            m_Analyzer.Initialize(KeyerResources.GetInstance());
            m_Analyzer.Completed += OnCompleted;
        }

        void OnDisable()
        {
            m_Analyzer.Completed -= OnCompleted;
            m_Analyzer.Dispose();

            StopAllCoroutines();

            Utilities.DeallocateIfNeeded(ref m_Capture);
        }

        void OnCompleted(Color keyingColor)
        {
            m_EstimatedKeyingColor = keyingColor;
            m_KeyingColorChanged = true;
        }

        [ContextMenu("Extract Keying Color")]
        void ExtractKeyingColor()
        {
            StopAllCoroutines();
            StartCoroutine(CaptureAndAnalyze());
        }

        IEnumerator CaptureAndAnalyze()
        {
            yield return new WaitForEndOfFrame();
            Utilities.AllocateIfNeeded(ref m_Capture, Screen.width, Screen.height);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(m_Capture);

            var cmd = CommandBufferPool.Get("Extract Keying Color");

            m_Analyzer.Execute(cmd, m_Capture,
                m_Settings.ReferenceKeyingColor,
                m_Settings.ApplyGammaToSource,
                m_Settings.BatchCount,
                m_Settings.BatchSize);


            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        [ContextMenu("Monitor Batch Parameters")]
        void ExecuteMonitorBatchParameters()
        {
            StopAllCoroutines();
            StartCoroutine(MonitorBatchParameters());
        }

        IEnumerator MonitorBatchParameters()
        {
            Color biasedReferenceColor = new Color(
                m_ReferenceKeyingColor.r + m_ReferenceKeyingColorBias.x,
                m_ReferenceKeyingColor.g + m_ReferenceKeyingColorBias.y,
                m_ReferenceKeyingColor.b + m_ReferenceKeyingColorBias.z);

            m_Samples.Clear();

            m_KeyingColorChanged = false;

            var mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera);
            mainCamera.GetComponent<HDAdditionalCameraData>().backgroundColorHDR = biasedReferenceColor;
            m_Settings.ReferenceKeyingColor = m_ReferenceKeyingColor;

            // Add a noise that does not affect the average color.
            m_ConfigureNoiseMaterial.UvScale = 1;
            m_ConfigureNoiseMaterial.Amount = 0.05f;
            m_ConfigureNoiseMaterial.Offset = 0;

            for (var i = 2; i <= 32; ++i)
            {
                m_Settings.BatchSize = i;

                foreach (var batchCount in k_BatchCounts)
                {
                    m_Settings.BatchCount = batchCount;
                    yield return CaptureAndAnalyze();

                    // Wait for sample.
                    while (!m_KeyingColorChanged)
                    {
                        yield return null;
                    }

                    m_KeyingColorChanged = false;

                    m_Samples.Add(new Sample
                    {
                        BatchSize = i,
                        BatchCount = batchCount,
                        KeyingColor = m_EstimatedKeyingColor
                    });
                }
            }

            WriteCsvReport(biasedReferenceColor, nameof(MonitorBatchParameters));
        }

        void WriteCsvReport(Color referenceColor, string fileName)
        {
            m_StringBuilder.Clear();
            m_StringBuilder.AppendLine("BatchSize,BatchCount,Error");

            for (var i = 0; i != m_Samples.Count; ++i)
            {
                var sample = m_Samples[i];
                var distance = Distance(sample.KeyingColor, referenceColor);

                m_StringBuilder.AppendLine($"{sample.BatchSize},{(int)sample.BatchCount},{distance}");
            }

            var directoryPath = Path.Combine(Application.dataPath, "TestResults");

            WriteTextFile(m_StringBuilder.ToString(), directoryPath, fileName);
        }

        static void WriteTextFile(string content, string directory, string fileName)
        {
            var filePath = Path.Combine(directory, fileName + ".csv");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
        }

        static float Distance(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        }
    }
}
