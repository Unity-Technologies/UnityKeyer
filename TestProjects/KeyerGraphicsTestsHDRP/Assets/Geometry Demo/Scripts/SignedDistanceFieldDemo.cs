using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer.Geometry.Demo
{
    public class SignedDistanceFieldDemo : MonoBehaviour
    {
        const string k_DisplayName = "Signed Distance Field";
        static readonly Rect k_GuiRect = new(12, 0, 512, 512);

        enum Display : byte
        {
            SDF,
            Dilate
        }

        struct Stats
        {
            public double ElapsedMs;
            public double GpuElapsedMs;
            public long BlockCount;
            public long GpuBlockCount;
        }

        [SerializeField]
        Texture2D m_Input;
        [SerializeField, Range(1, 64)]
        float m_Scale;
        [SerializeField]
        SignedDistanceField.Settings m_Settings;
        [SerializeField]
        bool m_UseDilate;
        [SerializeField, Range(0, 1)]
        float m_DilateThreshold;
        [SerializeField, Range(.1f, 4)]
        float m_GuiScale;
        [SerializeField]
        Display m_Display;

        readonly SignedDistanceField m_SignedDistanceField = new();
        readonly StringBuilder m_StringBuilder = new();
        CustomSampler m_Sampler;
        CommandBuffer m_SdfCommandBuffer;
        RenderTexture m_SdfResult;
        RenderTexture m_DilateResult;
        Material m_DilateMaterial;
        MaterialPropertyBlock m_PropertyBlock;
        Stats m_Stats;
        bool m_NeedsUpdate;

        void OnGUI()
        {
            var output = GetOutput();

            if (output != null)
            {
                var rectScale = Mathf.Min(m_GuiScale);
                var texCoordScale = (Mathf.Max(1, m_GuiScale) - 1) / 4;
                var rectMin = Vector2.Lerp(Vector2.zero, Vector2.one * .5f, texCoordScale);
                var rectMax = Vector2.Lerp(Vector2.one, Vector2.one * .5f, texCoordScale);
                var texCoords = Rect.MinMaxRect(rectMin.x, rectMin.y, rectMax.x, rectMax.y);
                var rect = new Rect(0, 0,
                    output.width * rectScale,
                    output.height * rectScale);
                GUI.DrawTextureWithTexCoords(rect, output, texCoords, false);
            }

            DrawStats();
        }

        void OnEnable()
        {
            m_NeedsUpdate = true;
            InitializeSampler(k_DisplayName);

            m_SdfCommandBuffer = new CommandBuffer
            {
                name = k_DisplayName
            };

            m_SignedDistanceField.Initialize();
            m_DilateMaterial = Media.Keyer.Utilities.CreateMaterial("Hidden/Keyer/GarbageMask2d");
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        void OnDisable()
        {
            Media.Keyer.Utilities.Destroy(m_DilateMaterial);

            m_SignedDistanceField.Dispose();

            m_SdfCommandBuffer.Dispose();

            if (m_SdfResult != null)
            {
                m_SdfResult.Release();
                m_SdfResult = null;
            }

            if (m_DilateResult != null)
            {
                m_DilateResult.Release();
                m_DilateResult = null;
            }
        }

        void OnValidate()
        {
            m_NeedsUpdate = true;

            // Keep SDF and dilate scale synced.
            m_Settings.Scale = m_Scale;
            m_Settings.Validate();
        }

        void Update()
        {
            if (m_NeedsUpdate)
            {
                Execute(false);
                UpdateStats();
                m_NeedsUpdate = false;
            }
        }

        // To compare the impact of using tiled rendering while preserving workload.
        [ContextMenu("Toggle Use Group")]
        void ToggleUseGroup()
        {
            m_Settings.UseSkipLodSteps = false;
            if (m_Settings.UseGroupsShared)
            {
                m_Settings.StepsPerLod[0] = m_Settings.GroupsSharedPasses;
                m_Settings.UseGroupsShared = false;
            }
            else
            {
                m_Settings.GroupsSharedPasses = m_Settings.StepsPerLod[0];
                m_Settings.StepsPerLod[0] = 1;
                m_Settings.UseGroupsShared = true;
            }

            m_Settings.Validate();
            m_NeedsUpdate = true;
        }

        [ContextMenu("Execute Capture")]
        void ExecuteCapture() => Execute(true);

        void Execute(bool renderDocCapture)
        {
            if (m_Input == null)
            {
                return;
            }

            AllocateIfNeeded(ref m_SdfResult, m_Input.width, m_Input.height, GraphicsFormat.R32_SFloat);
            m_SdfResult.filterMode = FilterMode.Point;

            if (m_UseDilate)
            {
                AllocateIfNeeded(ref m_DilateResult, m_Input.width, m_Input.height, GraphicsFormat.R16_UNorm);
            }

            if (renderDocCapture)
            {
                using var renderDocCaptureScope = EditorBridge.CreateRenderDocCaptureScope();
                Render();
            }
            else
            {
                Render();
            }
        }

        void Render()
        {
            m_SdfCommandBuffer.BeginSample(m_Sampler);
            m_SignedDistanceField.Execute(m_SdfCommandBuffer, m_Input, m_SdfResult, m_Settings);
            m_SdfCommandBuffer.EndSample(m_Sampler);

            if (m_UseDilate)
            {
                m_PropertyBlock.SetVector(ShaderIDs._GarbageMaskParams, new Vector3(m_DilateThreshold, 0, 1));
                m_SdfCommandBuffer.SetRenderTarget(m_DilateResult);
                // Note the shader pass selected.
                Blitter.Blit(m_DilateMaterial, m_PropertyBlock, m_SdfCommandBuffer, m_SdfResult, new(1, 1, 0, 0), new(1, 1, 0, 0), 2);
            }

            Graphics.ExecuteCommandBuffer(m_SdfCommandBuffer);
            m_SdfCommandBuffer.Clear();
        }

        void DrawStats()
        {
            var mtx = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * 2);
            GUI.color = Color.blue;

            // Draw profiling stats.
            m_StringBuilder.Clear();
            m_StringBuilder.AppendLine("Profiling:");
            m_StringBuilder.AppendLine($"ElapsedMs: {m_Stats.ElapsedMs}");
            m_StringBuilder.AppendLine($"GpuElapsedMs: {m_Stats.GpuElapsedMs}");
            m_StringBuilder.AppendLine($"BlockCount: {m_Stats.BlockCount}");
            m_StringBuilder.AppendLine($"GpuBlockCount: {m_Stats.GpuBlockCount}");

            GUI.Label(k_GuiRect, m_StringBuilder.ToString());
            GUI.matrix = mtx;
        }

        void InitializeSampler(string name)
        {
            if (m_Sampler == null)
            {
                m_Sampler = CustomSampler.Create(name, true);
            }
        }

        void UpdateStats()
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

        [ContextMenu("Save Output As Png")]
        void SaveOutputAsPng()
        {
            var output = GetOutput();

            if (output != null)
            {
                var info = Directory.GetParent(Application.dataPath);
                var path = Path.Combine(info.FullName, $"{m_Display}.png");
                Media.Keyer.Utilities.SaveAsPng(output, path);
            }
        }

        RenderTexture GetOutput()
        {
            switch (m_Display)
            {
                case Display.SDF:
                    return m_SdfResult;
                case Display.Dilate:
                    return m_DilateResult;
            }

            return null;
        }

        // These utilities duplication across packages and demos call for thinking.
        static void AllocateIfNeeded(ref RenderTexture rt, int width, int height, GraphicsFormat format)
        {
            if (rt == null ||
                rt.width != width ||
                rt.height != height ||
                rt.graphicsFormat != format)
            {
                if (rt != null)
                {
                    rt.Release();
                }

                rt = new RenderTexture(width, height, 0, format, 0);
                rt.enableRandomWrite = true; // Always?
                rt.Create();
            }
        }
    }
}
