using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct Context
    {
        public Shaders Shaders;
        public KernelIds.RenderingIds KernelIds;
    }

    /// <summary>
    /// An interface allowing property drawers to access a Keyer linked to a Display property.
    /// </summary>
    public interface IKeyerAccess
    {
        /// <summary>
        /// Returns Keyer component.
        /// </summary>
        /// <returns>Keyer component.</returns>
        Keyer GetKeyer();

        /// <summary>
        /// Event occurs when keyer settings changed.
        /// </summary>
        event Action Changed;
    }

    /// <summary>
    /// Keyer component class for the Multipass Keyer.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Keyer/Keyer")]
    [Serializable]
    public class Keyer : MonoBehaviour, IKeyerAccess
    {
        const int k_WarpSize = 8;

        /// <summary>
        /// Display mode for the Keyer.
        /// </summary>
        public enum Display : byte
        {
            /// <summary>
            /// Display the resulting image with the Key applied to it.
            /// </summary>
            Result = 0,
            /// <summary>
            /// Display the Core Matte intermediate result.
            /// </summary>
            CoreMatte = 1,
            /// <summary>
            /// Display the Soft Matte intermediate result.
            /// </summary>
            SoftMatte = 2,
            /// <summary>
            /// Display the Erode Matte intermediate result.
            /// </summary>
            ErodeMatte = 3,
            /// <summary>
            /// Display the Blend Max intermediate result.
            /// </summary>
            BlendMax = 4,
            /// <summary>
            /// Display the Original input front image.
            /// </summary>
            Front = 5,
            /// <summary>
            /// Display the applied Garbage Mask.
            /// </summary>
            GarbageMask = 6,
            /// <summary>
            /// Display Despill intermediate result.
            /// </summary>
            Despill = 7,
            /// <summary>
            /// Display the Crop Matte Mask.
            /// </summary>
            CropMatte = 8
        }

        // Tracking active instances, used for UI.
        static readonly List<Keyer> s_ActiveInstances = new();
        internal static IReadOnlyList<Keyer> ActiveInstances => s_ActiveInstances;
        internal static event Action ActiveInstancesChanged = delegate { };

        // We clear on release to avoid keeping around references to released textures.
        static readonly ObjectPool<List<RenderTexture>> s_RenderTextureListPool = new(() => new List<RenderTexture>(), null, x => x.Clear());

        [SerializeField]
        Texture m_Foreground;
        [SerializeField]
        RenderTexture m_Result;
        [SerializeField]
        Display m_DisplayMode;
        [SerializeField]
        KeyerSettings m_Settings;

        // Track handles used for capture.
        readonly Dictionary<Display, TextureHandle> m_CaptureInputs = new();
        readonly Dictionary<Display, List<RenderTexture>> m_CaptureOutputs = new();

        readonly GarbageMaskSystem m_GarbageMaskSystem = new();
        readonly RenderGraph m_RenderGraph = new();

        bool m_GarbageMaskNeedsRender;
        bool m_NeedsRenderGraphRebuild;
        bool m_PendingRenderDocCapture;
        bool m_PendingSdfRenderDocCapture;
        int m_PreviousSettingsHash;
        int m_ActiveCaptures;

        CustomSampler m_UpdateSampler;
        CommandBuffer m_CommandBuffer;

        // Implementation of IKeyerAccess.

        /// <summary>
        /// Returns Keyer component.
        /// </summary>
        /// <returns>Keyer component.</returns>
        Keyer IKeyerAccess.GetKeyer() => this;

        Action m_Changed = delegate { };

        event Action IKeyerAccess.Changed
        {
            add => m_Changed += value;
            remove => m_Changed -= value;
        }

        // So that the generator is accessible to the editor tool.
        internal GarbageMaskSystem GarbageMaskSystem => m_GarbageMaskSystem;

        internal void RequestGarbageMaskGeneration() => m_GarbageMaskNeedsRender = true;

        internal void RequestRenderDocCapture() => m_PendingRenderDocCapture = true;
        internal void RequestSdfRenderDocCapture() => m_PendingSdfRenderDocCapture = true;

        /// <summary>
        /// Property for the Keyer Settings.
        /// </summary>
        public KeyerSettings Settings
        {
            get => m_Settings;
            set
            {
                m_NeedsRenderGraphRebuild |= m_Settings != value;
                m_Settings = value;
            }
        }

        /// <summary>
        /// The input foreground texture.
        /// </summary>
        public Texture Foreground
        {
            get => m_Foreground;
            set
            {
                m_NeedsRenderGraphRebuild |= m_Foreground != value;
                m_Foreground = value;
            }
        }

        /// <summary>
        /// The Keyer's resulting render texture.
        /// </summary>
        public RenderTexture Result
        {
            get => m_Result;
            set
            {
                m_NeedsRenderGraphRebuild |= m_Result != value;
                m_Result = value;
            }
        }

        /// <summary>
        /// Display Mode selects which intermediate results of the Keyer to display.
        /// </summary>
        public Display DisplayMode
        {
            get => m_DisplayMode;
            set => m_DisplayMode = value;
        }

        /// <summary>
        /// Registers a capture output.
        /// </summary>
        /// <param name="display">The display identifying the render pass to be captured.</param>
        /// <param name="target">The render texture the capture should be written to.</param>
        internal void AddCapture(Display display, RenderTexture target)
        {
            var list = default(List<RenderTexture>);
            if (m_CaptureOutputs.TryGetValue(display, out list))
            {
                if (list.Contains(target))
                {
                    throw new InvalidOperationException(
                        $"Render texture {target} is already registered to capture {display}.");
                }

                list.Add(target);
            }
            else
            {
                list = s_RenderTextureListPool.Get();
                list.Add(target);
                m_CaptureOutputs.Add(display, list);
            }

            ++m_ActiveCaptures;
            m_NeedsRenderGraphRebuild = true;
        }

        /// <summary>
        /// Removes a registered capture output.
        /// </summary>
        /// <param name="display">The display associated with the capture.</param>
        /// <param name="target">The render texture target associated with the capture.</param>
        /// <returns>True if the target was found and removed, false otherwise.</returns>
        internal bool RemoveCapture(Display display, RenderTexture target)
        {
            if (m_CaptureOutputs.TryGetValue(display, out var list) && list.Remove(target))
            {
                --m_ActiveCaptures;
                m_NeedsRenderGraphRebuild = true;
                return true;
            }

            return false;
        }

        void OnEnable()
        {
            if (m_UpdateSampler == null)
            {
                m_UpdateSampler = CustomSampler.Create("Keyer Update");
            }

            m_CommandBuffer = new CommandBuffer();
            m_CommandBuffer.name = "Keyer";

            m_RenderGraph.Initialize();
            m_GarbageMaskSystem.Initialize();

            m_NeedsRenderGraphRebuild = true;

            s_ActiveInstances.Add(this);
            ActiveInstancesChanged.Invoke();
        }

        void OnDisable()
        {
            s_ActiveInstances.Remove(this);
            ActiveInstancesChanged.Invoke();

            m_GarbageMaskSystem.Dispose();
            m_CaptureInputs.Clear();
            m_CommandBuffer.Dispose();
            m_RenderGraph.Dispose();

            foreach (var item in m_CaptureOutputs)
            {
                s_RenderTextureListPool.Release(item.Value);
            }

            m_CaptureOutputs.Clear();
            m_ActiveCaptures = 0;
        }

        void Update()
        {
            if (!CanRender())
            {
                return;
            }

            using var samplerScope = new CustomSamplerScope(m_UpdateSampler);

            // Trigger a render graph rebuild if settings changed.
            var currentHash = m_Settings.GetSerializedFieldsHashCode();
            var settingsChanged = m_PreviousSettingsHash != currentHash;
            m_NeedsRenderGraphRebuild |= settingsChanged;
            m_PreviousSettingsHash = currentHash;

            // When capturing we force a mask generation.
            m_GarbageMaskNeedsRender |= m_PendingRenderDocCapture;
            m_NeedsRenderGraphRebuild |= m_GarbageMaskNeedsRender;

            var size = new Vector2Int(m_Foreground.width, m_Foreground.height);
            var garbageMask = m_GarbageMaskSystem.Update(
                m_CommandBuffer, m_Settings.GarbageMask, size, ref m_GarbageMaskNeedsRender, out var sdfRender);

            m_PendingRenderDocCapture |= sdfRender && m_PendingSdfRenderDocCapture;

            if (m_NeedsRenderGraphRebuild)
            {
                BuildRenderGraph(garbageMask);
                m_NeedsRenderGraphRebuild = false;
            }

            m_RenderGraph.Execute(m_CommandBuffer);

            if (settingsChanged)
            {
                m_Changed.Invoke();
            }
        }

        void LateUpdate()
        {
            // m_PendingSdfRenderDocCapture should not be set to false until m_PendingRenderDocCapture has been true.
            if (m_PendingRenderDocCapture)
            {
                using var captureScope = EditorBridge.CreateRenderDocCaptureScope();
                Graphics.ExecuteCommandBuffer(m_CommandBuffer);
                m_PendingRenderDocCapture = false;
                m_PendingSdfRenderDocCapture = false;
            }
            else
            {
                Graphics.ExecuteCommandBuffer(m_CommandBuffer);
            }

            m_CommandBuffer.Clear();
        }

        internal void OnValidate()
        {
            m_NeedsRenderGraphRebuild = true;
            m_Changed.Invoke();
        }

        bool CanRender()
        {
            // TODO We should have warning messages in custom Inspectors.
            if (Foreground == null)
            {
                Debug.LogWarning($"{nameof(Foreground)} texture is not assigned");
                return false;
            }

            if (m_Result == null)
            {
                Debug.LogWarning($"{nameof(Result)} texture is not assigned");
                return false;
            }

            if (m_Settings == null)
            {
                Debug.LogWarning("Keyer Settings is not assigned");
                return false;
            }

            return true;
        }

        // Must be invoked in response to public fields changing.
        // In a production scenario we expect the render graph to essentially be built once.
        // It gets rebuilt a lot when configuring the keyer but this is ok.
        void BuildRenderGraph(Texture garbageMask)
        {
            // Reset Capture Inputs.
            m_CaptureInputs.Clear();

            using (var builder = m_RenderGraph.GetBuilder(Foreground.width, Foreground.height))
            {
                var garbageMaskEnabled = m_Settings.GarbageMask.Enabled && garbageMask != null;

                // TODO User should not be able to select GarbageMask display is no GarbageMask is set.
                // This is temporary.
                // Should be handled in a custom inspector for example.
                if (!garbageMaskEnabled && m_DisplayMode == Display.GarbageMask)
                {
                    m_DisplayMode = Display.Result;
                }

                // Handle static texture Capture. No need to add more render passes afterwards.
                // Only if we have no capture pending.
                if (m_ActiveCaptures == 0)
                {
                    switch (m_DisplayMode)
                    {
                        case Display.Front:
                        {
                            AddCopyPass(builder, Foreground, m_Result);
                            return;
                        }
                        case Display.GarbageMask:
                        {
                            AddCopyPass(builder, m_Settings.GarbageMask.Texture, m_Result);
                            return;
                        }
                    }
                }

                AddRenderPasses(builder, garbageMask, garbageMaskEnabled);
            }
        }

        void AddRenderPasses(RenderGraph.IBuilder builder, Texture garbageMask, bool garbageMaskEnabled)
        {
            var output = default(TextureHandle);
            var foregroundOutput = builder.Handles.FromTexture(Foreground);
            m_CaptureInputs.Add(Display.Front, foregroundOutput);

            // Core mask.
            var coreMattePass = default(IRenderPass);
            if (m_Settings.SegmentationAlgorithmCore == SegmentationAlgorithm.ColorDifference)
            {
                coreMattePass = new ColorDifferencePass(new ColorDifferencePassData
                {
                    Input = builder.Handles.FromTexture(Foreground),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "colorDifferenceCore"),
                    BackgroundChannel = m_Settings.ColorDifferenceCoreMask.BackgroundColor,
                    Scale = m_Settings.ColorDifferenceCoreMask.Scale,
                    Clip = new Vector2(m_Settings.ColorDifferenceCoreMask.ClipBlack, m_Settings.ColorDifferenceCoreMask.ClipWhite)
                });
            }
            else
            {
                coreMattePass = new ColorDistancePass(new ColorDistancePassData
                {
                    Input = builder.Handles.FromTexture(Foreground),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "colorDistanceCore"),
                    ChromaKey = m_Settings.ColorDistanceCoreMask.ChromaKey,
                    Threshold = m_Settings.ColorDistanceCoreMask.Threshold,
                });
            }

            builder.AddPass(coreMattePass);
            m_CaptureInputs.Add(Display.CoreMatte, coreMattePass.Output);
            output = coreMattePass.Output;

            // Blur.
            // Note that we only schedule the blur pass if it has an effect.
            // There's no point in using more samples than the radius calls for.
            var blurSampleCount = Mathf.Min((int)m_Settings.BlurMask.Quality, Mathf.RoundToInt(m_Settings.BlurMask.Radius));
            var hasBlur = m_Settings.BlurMask.Enabled && m_Settings.BlurMask.Radius > Mathf.Epsilon && blurSampleCount > 1;
            var blurPass = default(BlurPass);
            if (hasBlur)
            {
                blurPass = new BlurPass(new BlurPassData
                {
                    Input = builder.Handles.CreateReadTransient(coreMattePass.Output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "blur"),
                    Temp = builder.Handles.CreateTempTransient(BufferFormat.R, "blurTemp"),
                    Radius = m_Settings.BlurMask.Radius,
                    SampleCount = blurSampleCount
                });
                builder.AddPass(blurPass);
                output = blurPass.Output;
            }

            var clipPass = default(IRenderPass);
            if (m_Settings.ClipMask.Enabled)
            {
                // Clip.
                clipPass = new ClipPass(new ClipPassData
                {
                    Input = builder.Handles.CreateReadTransient(output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "clip"),
                    Range = new Vector2(m_Settings.ClipMask.ClipBlack, m_Settings.ClipMask.ClipWhite)
                });
                builder.AddPass(clipPass);
                m_CaptureInputs.Add(Display.ErodeMatte, clipPass.Output);
                output = clipPass.Output;
            }

            var softMattePass = default(IRenderPass);
            var blendPass = default(IRenderPass);

            if (m_Settings.SoftMaskEnabled)
            {
                // Soft mask.
                if (m_Settings.SegmentationAlgorithmSoft == SegmentationAlgorithm.ColorDifference)
                {
                    softMattePass = new ColorDifferencePass(new ColorDifferencePassData
                    {
                        Input = builder.Handles.FromTexture(Foreground),
                        Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "colorDifferenceSoft"),
                        BackgroundChannel = m_Settings.ColorDifferenceSoftMask.BackgroundColor,
                        Scale = m_Settings.ColorDifferenceSoftMask.Scale,
                        Clip = new Vector2(m_Settings.ColorDifferenceSoftMask.ClipBlack, m_Settings.ColorDifferenceSoftMask.ClipWhite)
                    });
                }
                else
                {
                    softMattePass = new ColorDistancePass(new ColorDistancePassData
                    {
                        Input = builder.Handles.FromTexture(m_Foreground),
                        Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "colorDistanceSoft"),
                        ChromaKey = m_Settings.ColorDistanceSoftMask.ChromaKey,
                        Threshold = m_Settings.ColorDistanceSoftMask.Threshold,
                    });
                }

                builder.AddPass(softMattePass);
                m_CaptureInputs.Add(Display.SoftMatte, softMattePass.Output);
                output = softMattePass.Output;
                if (m_Settings.BlendMask.Enabled)
                {
                    // Blend.
                    blendPass = new BlendMaxPass(new BlendMaxPassData
                    {
                        Background = builder.Handles.CreateReadTransient(softMattePass.Output),
                        Foreground = builder.Handles.CreateReadTransient(clipPass != null ? clipPass.Output : coreMattePass.Output),
                        Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "blendMax"),
                        Amount = m_Settings.BlendMask.Strength
                    });
                    builder.AddPass(blendPass);
                    m_CaptureInputs.Add(Display.BlendMax, blendPass.Output);
                    output = blendPass.Output;
                }
            }

            // GarbageMask.
            var garbageMaskPass = default(IRenderPass);
            if (garbageMaskEnabled)
            {
                // Note at the moment we use the inversion value of the core matte.
                garbageMaskPass = new GarbageMaskPass(new GarbageMaskPassData
                {
                    Input = builder.Handles.CreateReadTransient(output),
                    Mask = builder.Handles.FromTexture(garbageMask),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "garbage mask"),
                    SdfEnabled = m_Settings.GarbageMask.SdfEnabled,
                    SdfDistance = m_Settings.GarbageMask.SdfDistance,
                    Invert = m_Settings.GarbageMask.Invert ? 1 : 0,
                    Threshold = m_Settings.GarbageMask.Threshold,
                    Blend = m_Settings.GarbageMask.Blend
                });
                builder.AddPass(garbageMaskPass);
                m_CaptureInputs.Add(Display.GarbageMask, garbageMaskPass.Output);
                output = garbageMaskPass.Output;
            }

            var cropPass = default(IRenderPass);
            if (m_Settings.CropMask.Enabled)
            {
                // Crop.
                cropPass = new CropPass(new CropPassData
                {
                    Input = builder.Handles.CreateReadTransient(output),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.R, "crop"),
                    Rect = Rect.MinMaxRect(m_Settings.CropMask.Left, m_Settings.CropMask.Bottom,
                        1 - m_Settings.CropMask.Right, 1 - m_Settings.CropMask.Top)
                });
                builder.AddPass(cropPass);
                m_CaptureInputs.Add(Display.CropMatte, cropPass.Output);
                output = cropPass.Output;
            }

            if (m_Settings.Despill.Enabled)
            {
                // Despill.
                var despillPass = new DespillPass(new DespillPassData
                {
                    Input = builder.Handles.FromTexture(Foreground),
                    Output = builder.Handles.CreateWriteTransient(BufferFormat.RGBA, "despill"),
                    BackgroundChannel = m_Settings.Despill.BackgroundColor,
                    Amount = m_Settings.Despill.DespillAmount,
                });
                builder.AddPass(despillPass);
                m_CaptureInputs.Add(Display.Despill, despillPass.Output);
                foregroundOutput = despillPass.Output;
            }

            // Can we write directly to the result texture from the Combine compute shader?
            var combineWritesToResult = m_Result.enableRandomWrite && m_DisplayMode == Display.Result;

            var colorOutput = m_Settings.Despill.Enabled ? builder.Handles.CreateReadTransient(foregroundOutput) : foregroundOutput;

            // Combine.
            var combinePass = new CombineColorAndAlphaPass(new CombineColorAndAlphaPassData
            {
                Color = colorOutput,
                Alpha = builder.Handles.CreateReadTransient(output),
                Output = combineWritesToResult ? builder.Handles.FromTexture(m_Result) : builder.Handles.CreateWriteTransient(BufferFormat.RGBA, "combine"),
            });
            builder.AddPass(combinePass);
            m_CaptureInputs.Add(Display.Result, combinePass.Output);

            // If the result texture cannot be written to by a compute shader, we'll need an additional copy.
            if (!combineWritesToResult)
            {
                // Select source for Capture.
                // Note that useless passes will be pruned from the render graph.
                var source = default(TextureHandle);
                switch (m_DisplayMode)
                {
                    case Display.Result:
                        source = combinePass.Output;
                        break;
                    case Display.CoreMatte:
                        source = coreMattePass.Output;
                        break;
                    case Display.SoftMatte:
                        source = m_Settings.SoftMaskEnabled ? softMattePass.Output : coreMattePass.Output;
                        break;
                    case Display.ErodeMatte:
                        source = m_Settings.ClipMask.Enabled ? clipPass.Output : coreMattePass.Output;
                        break;
                    case Display.BlendMax:
                        source = (m_Settings.BlendMask.Enabled && m_Settings.SoftMaskEnabled) ? blendPass.Output : coreMattePass.Output;
                        break;
                    case Display.Front:
                        source = builder.Handles.FromTexture(Foreground);
                        break;
                    case Display.GarbageMask:
                        source = garbageMaskEnabled ? garbageMaskPass.Output : builder.Handles.FromTexture(m_Settings.GarbageMask.Texture == null ? Texture2D.blackTexture : m_Settings.GarbageMask.Texture);
                        break;
                    case Display.Despill:
                        source = m_Settings.Despill.Enabled ? foregroundOutput : builder.Handles.FromTexture(Foreground);
                        break;
                    case Display.CropMatte:
                        source = m_Settings.CropMask.Enabled ? cropPass.Output : coreMattePass.Output;
                        break;
                }

                // Present pass.
                AddCopyPass(builder, source, m_Result);
            }

            // Add capture passes.
            foreach (var (display, targets) in m_CaptureOutputs)
            {
                if (m_CaptureInputs.TryGetValue(display, out var source))
                {
                    foreach (var target in targets)
                    {
                        AddCopyPass(builder, source, target);
                    }
                }
            }
        }

        static void AddCopyPass(RenderGraph.IBuilder builder, TextureHandle source, RenderTexture destination)
        {
            var copyPassData = new CopyPassData
            {
                Input = source.Type == ResourceType.Transient ? builder.Handles.CreateReadTransient(source) : source,
                Output = builder.Handles.FromTexture(destination)
            };
            builder.AddPass(new CopyPass(copyPassData));
        }

        static void AddCopyPass(RenderGraph.IBuilder builder, Texture source, RenderTexture destination)
        {
            var copyPassData = new CopyPassData
            {
                Input = builder.Handles.FromTexture(source),
                Output = builder.Handles.FromTexture(destination)
            };
            builder.AddPass(new CopyPass(copyPassData));
        }
    }
}
