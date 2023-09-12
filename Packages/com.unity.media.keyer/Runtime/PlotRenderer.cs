using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    static class ChannelsExtensions
    {
        public static Vector3 AsVector3(this PlotRenderer.Channels channels)
        {
            return new(
                channels.HasFlag(PlotRenderer.Channels.Red) ? 1 : 0,
                channels.HasFlag(PlotRenderer.Channels.Green) ? 1 : 0,
                channels.HasFlag(PlotRenderer.Channels.Blue) ? 1 : 0);
        }
    }

    [Serializable]
    class PlotRenderer : IDisposable
    {
        public enum CurveStyle : byte
        {
            Outline = 0,
            Filled = 1
        }

        [Flags]
        public enum Channels : byte
        {
            None = 0,
            Red = 1 << 0,
            Green = 1 << 1,
            Blue = 1 << 2,
            All = Red | Green | Blue
        }

        public interface IFunction
        {
            /// <summary>
            /// Evaluates the function at a given time.
            /// </summary>
            /// <param name="t">Normalized time value.</param>
            /// <returns>Function result at time t.</returns>
            float Evaluate(float t);
        }

        interface IRenderPassInternal
        {
            byte Id { get; }
            bool IsValid();
            void Prepare(int numSamples);
            void Execute(CommandBuffer cmd, Material material, MaterialPropertyBlock propertyBlock, int numSamples);
            void Dispose();
        }

        public interface IPlot
        {
            bool Enabled { get; set; }

            // 256 plots is largely sufficient.
            byte Id { get; set; }
        }

        public interface IFunctionPlot<T> : IPlot where T : struct, IFunction
        {
            T Function { get; set; }
            Color Color { get; set; }
            CurveStyle CurveStyle { get; set; }
        }

        class FunctionRenderPass<T> : IRenderPassInternal, IFunctionPlot<T> where T : struct, IFunction
        {
            struct EvaluateFunction<T1> : IJobParallelFor where T1 : struct, IFunction
            {
                public float IndexToTime;
                public T1 Function;
                public NativeArray<float> Result;

                public void Execute(int i)
                {
                    Result[i] = Function.Evaluate(i * IndexToTime);
                }
            }

            ComputeBuffer m_Buffer;
            T m_Function;
            Color m_Color;
            CurveStyle m_CurveStyle;
            byte m_Id;
            bool m_Enabled;

            public T Function
            {
                get => m_Function;
                set => m_Function = value;
            }

            public bool Enabled
            {
                get => m_Enabled;
                set => m_Enabled = value;
            }

            public byte Id
            {
                get => m_Id;
                set => m_Id = value;
            }

            public Color Color
            {
                get => m_Color;
                set => m_Color = value;
            }

            public CurveStyle CurveStyle
            {
                get => m_CurveStyle;
                set => m_CurveStyle = value;
            }

            public bool IsValid() => m_Enabled;

            public void Prepare(int numSamples)
            {
                // Sort of a heuristic,
                // for small numbers of samples, it's not worth scheduling a job.
                if (numSamples > 128)
                {
                    PopulateBufferUsingJob(numSamples);
                }
                else
                {
                    PopulateBuffer(numSamples);
                }
            }

            public void Execute(CommandBuffer cmd, Material material, MaterialPropertyBlock propertyBlock, int numSamples)
            {
                propertyBlock.SetBuffer(ShaderIDs._SourceBuffer, m_Buffer);
                propertyBlock.SetColor(ShaderIDs._Color, m_Color);

                cmd.DrawProcedural(Matrix4x4.identity, material, 4 + (int)m_CurveStyle, MeshTopology.Quads, 4, 1, propertyBlock);
            }

            public void Dispose()
            {
                Utilities.DeallocateIfNeeded(ref m_Buffer);
            }

            void PopulateBuffer(int numSamples)
            {
                var data = new NativeArray<float>(numSamples, Allocator.Temp);

                for (var i = 0; i != numSamples; ++i)
                {
                    data[i] = m_Function.Evaluate(i / (float)(numSamples - 1));
                }

                Utilities.AllocateBufferIfNeeded(ref m_Buffer, data);
            }

            void PopulateBufferUsingJob(int numSamples)
            {
                var data = new NativeArray<float>(numSamples, Allocator.TempJob);

                var job = new EvaluateFunction<T>
                {
                    IndexToTime = 1 / (float)(numSamples - 1),
                    Function = m_Function,
                    Result = data
                };

                var handle = job.Schedule(numSamples, 1);
                handle.Complete();

                Utilities.AllocateBufferIfNeeded(ref m_Buffer, data);

                data.Dispose();
            }
        }

        class BaseTexturePass
        {
            protected Texture m_Source;
            protected Vector2 m_StartPoint;
            protected Vector2 m_EndPoint;
            protected CurveStyle m_CurveStyle;

            bool m_Enabled;
            byte m_Id;

            public byte Id
            {
                get => m_Id;
                set => m_Id = value;
            }

            public bool Enabled
            {
                get => m_Enabled;
                set => m_Enabled = value;
            }

            public Texture Source
            {
                get => m_Source;
                set => m_Source = value;
            }

            public Vector2 StartPoint
            {
                get => m_StartPoint;
                set => m_StartPoint = value;
            }

            public Vector2 EndPoint
            {
                get => m_EndPoint;
                set => m_EndPoint = value;
            }

            public CurveStyle CurveStyle
            {
                get => m_CurveStyle;
                set => m_CurveStyle = value;
            }

            public bool IsValid() => m_Enabled && m_Source != null;
        }

        public interface IRgbPlot : IPlot
        {
            Texture Source { get; set; }
            Vector2 StartPoint { get; set; }
            Vector2 EndPoint { get; set; }
            Channels Channels { get; set; }
            CurveStyle CurveStyle { get; set; }
        }

        class RgbRenderPass : BaseTexturePass, IRenderPassInternal, IRgbPlot
        {
            Channels m_Channels;

            public Channels Channels
            {
                get => m_Channels;
                set => m_Channels = value;
            }

            public void Prepare(int numSamples) { }

            public void Execute(CommandBuffer cmd, Material material, MaterialPropertyBlock propertyBlock, int numSamples)
            {
                propertyBlock.SetTexture(ShaderIDs._SourceTexture, m_Source);
                propertyBlock.SetVector(ShaderIDs._Line,
                    new Vector4(m_StartPoint.x, m_StartPoint.y, m_EndPoint.x, m_EndPoint.y));
                propertyBlock.SetVector(ShaderIDs._Channels, m_Channels.AsVector3());

                cmd.DrawProcedural(Matrix4x4.identity, material, (int)m_CurveStyle, MeshTopology.Quads, 4, 1, propertyBlock);
            }

            public void Dispose() { }
        }

        public interface ISingleChannelPlot : IPlot
        {
            Texture Source { get; set; }
            Vector2 StartPoint { get; set; }
            Vector2 EndPoint { get; set; }
            Color Color { get; set; }
            CurveStyle CurveStyle { get; set; }
        }

        class SingleChannelRenderPass : BaseTexturePass, IRenderPassInternal, ISingleChannelPlot
        {
            Color m_Color;

            public Color Color
            {
                get => m_Color;
                set => m_Color = value;
            }

            public void Prepare(int numSamples) { }

            public void Execute(CommandBuffer cmd, Material material, MaterialPropertyBlock propertyBlock, int numSamples)
            {
                propertyBlock.SetTexture(ShaderIDs._SourceTexture, m_Source);
                propertyBlock.SetVector(ShaderIDs._Line,
                    new Vector4(m_StartPoint.x, m_StartPoint.y, m_EndPoint.x, m_EndPoint.y));
                propertyBlock.SetColor(ShaderIDs._Color, m_Color);

                cmd.DrawProcedural(Matrix4x4.identity, material, 2 + (int)m_CurveStyle, MeshTopology.Quads, 4, 1, propertyBlock);
            }

            public void Dispose() { }
        }

        const int k_GroupSize = 32;

        [SerializeField, Range(1, 8)]
        float m_Thickness = 1;

        [SerializeField, Range(1e-3f, 1)]
        float m_Smoothness = 1;

        [SerializeField, Range(0, 1)]
        float m_Opacity = 0.2f;

        [SerializeField]
        bool m_PickingEnabled;

        Material m_Material;
        MaterialPropertyBlock m_PropertyBlock;
        RenderTexture m_TempTarget;
        ComputeShader m_PickingShader;
        NativeArray<byte> m_IdBuffer;
        Vector2Int m_ReadbackSize;
        int m_BlendKernel;
        int m_PropagateKernel;

        readonly List<IRenderPassInternal> m_RenderPasses = new();
        readonly HashSet<byte> m_TrackIds = new();
        readonly DoubleRenderTexture m_IdTarget = new();
        readonly DoubleRenderTexture m_CoverageTarget = new();

        /// <summary>
        /// Activate or deactivate picking.
        /// </summary>
        /// <param name="value">The new picking activation value.</param>
        public void SetPickingEnabled(bool value)
        {
            if (m_PickingEnabled != value && !value)
            {
                DisposePickingData();
            }

            m_PickingEnabled = value;
        }

        T Create<T>() where T : class, IPlot, IRenderPassInternal, new()
        {
            var plot = new T
            {
                Enabled = true
            };
            m_RenderPasses.Add(plot);
            return plot;
        }

        public ISingleChannelPlot CreateSingleChannelPlot() => Create<SingleChannelRenderPass>();

        public IRgbPlot CreateRgbPlot() => Create<RgbRenderPass>();

        public IFunctionPlot<T> CreateFunctionPlot<T>() where T : struct, IFunction => Create<FunctionRenderPass<T>>();

        public bool RemovePlot(IPlot plot)
        {
            var pass = (IRenderPassInternal)plot;

            if (m_RenderPasses.Contains(pass))
            {
                m_RenderPasses.Remove(pass);
                pass.Dispose();
                return true;
            }

            return false;
        }

        public void Initialize()
        {
            m_PickingShader = KeyerResources.GetInstance().Shaders.PlotPicking;
            m_BlendKernel = KeyerResources.GetInstance().KernelIds.Rendering.BlendId;
            m_PropagateKernel = KeyerResources.GetInstance().KernelIds.Rendering.PropagateId;

            var shader = KeyerResources.GetInstance().Shaders.Plot;
            m_Material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (m_PropertyBlock == null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }
        }

        public void Dispose()
        {
            foreach (var pass in m_RenderPasses)
            {
                pass.Dispose();
            }

            m_RenderPasses.Clear();

            Utilities.Destroy(m_Material);
            DisposePickingData();
        }

        void DisposePickingData()
        {
            Utilities.DeallocateNativeArrayIfNeeded(ref m_IdBuffer);
            Utilities.DeallocateIfNeeded(ref m_TempTarget);
            m_IdTarget.Dispose();
            m_CoverageTarget.Dispose();
        }

        void BeforeRender(int numSamples, float width, float height)
        {
            foreach (var pass in m_RenderPasses)
            {
                if (pass.IsValid())
                {
                    pass.Prepare(numSamples);
                }
            }

            // Useless at the moment, since we do full screen.
            // But we anticipate RTHandles support by having it already,
            // and can also reuse our Blit vertex shader and uniforms.
            m_PropertyBlock.SetVector(ShaderIDs._SourceScaleBias, Utilities.IdentityScaleBias);
            m_PropertyBlock.SetVector(ShaderIDs._TargetScaleBias, Utilities.IdentityScaleBias);

            // Replicating Legacy handling of texture parameters.
            m_PropertyBlock.SetVector(ShaderIDs._TargetTexelSize,
                new Vector4(width, height, 1 / width, 1 / height));

            m_PropertyBlock.SetVector(ShaderIDs._PlotParams,
                new Vector4(m_Thickness, m_Smoothness, m_Opacity, numSamples));
        }

        public void Render(CommandBuffer cmd, RenderTexture target, int numSamples)
        {
            BeforeRender(numSamples, target.width, target.height);

            if (m_PickingEnabled)
            {
                RenderWithPicking(cmd, target, numSamples);
            }
            else
            {
                RenderWithoutPicking(cmd, target, numSamples);
            }
        }

        void RenderWithoutPicking(CommandBuffer cmd, RenderTexture target, int numSamples)
        {
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.black);

            foreach (var pass in m_RenderPasses)
            {
                if (pass.IsValid())
                {
                    pass.Execute(cmd, m_Material, m_PropertyBlock, numSamples);
                }
            }
        }

        void RenderWithPicking(CommandBuffer cmd, RenderTexture target, int numSamples)
        {
            Utilities.AllocateIfNeeded(ref m_TempTarget, target.width, target.height, target.graphicsFormat);

            m_IdTarget.AllocateIfNeededForCompute(target.width, target.height, GraphicsFormat.R8_UNorm);
            m_CoverageTarget.AllocateIfNeededForCompute(target.width, target.height, GraphicsFormat.R8_UNorm);

            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(false, true, Color.black);
            cmd.SetRenderTarget(m_IdTarget.In);
            cmd.ClearRenderTarget(false, true, Color.black);
            cmd.SetRenderTarget(m_CoverageTarget.In);
            cmd.ClearRenderTarget(false, true, Color.black);

            var shader = m_PickingShader;
            var kernel = m_BlendKernel;
            var groupsX = Mathf.CeilToInt(target.width / (float)k_GroupSize);
            var groupsY = Mathf.CeilToInt(target.height / (float)k_GroupSize);

            // No need to schedule a readback if no pass was executed.
            var scheduleReadback = false;
            m_TrackIds.Clear();

            foreach (var pass in m_RenderPasses)
            {
                // Sanity checks, regardless of pass validity.
                if (pass.Id == 0)
                {
                    throw new InvalidOperationException(
                        "Plot Ids must be non zero, as zero is reserved for empty space.");
                }

                if (m_TrackIds.Contains(pass.Id))
                {
                    throw new InvalidOperationException(
                        "Multiple plots have the same Id. Make sure the assigned Ids are unique.");
                }

                m_TrackIds.Add(pass.Id);

                if (pass.IsValid())
                {
                    scheduleReadback = true;

                    // Render to temp target.
                    // We need the isolated curve to evaluate picking ids.
                    cmd.SetRenderTarget(m_TempTarget);
                    cmd.ClearRenderTarget(false, true, Color.black);
                    pass.Execute(cmd, m_Material, m_PropertyBlock, numSamples);

                    // Update ids.
                    cmd.SetComputeFloatParam(shader, ShaderIDs._Id, pass.Id / 255.0f);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._SourceTexture, m_TempTarget);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._IdTexture, m_IdTarget.In);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._CoverageTexture, m_CoverageTarget.In);
                    cmd.DispatchCompute(shader, kernel, groupsX, groupsY, 1);

                    // Blend to final target.
                    cmd.SetRenderTarget(target);
                    Blitter.Blit(cmd, m_TempTarget, Blitter.Pass.Additive);
                }
            }

            if (scheduleReadback)
            {
                kernel = m_PropagateKernel;

                m_ReadbackSize = new Vector2Int(m_IdTarget.In.width, m_IdTarget.In.height);

                cmd.SetComputeVectorParam(shader, ShaderIDs._Size, (Vector2)m_ReadbackSize);

                // Run propagation to detect the closest curve rather than only the exact matches.
                for (var i = 0; i != 2; ++i)
                {
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._IdTextureIn, m_IdTarget.In);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._CoverageTextureIn, m_CoverageTarget.In);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._IdTextureOut, m_IdTarget.Out);
                    cmd.SetComputeTextureParam(shader, kernel, ShaderIDs._CoverageTextureOut, m_CoverageTarget.Out);
                    cmd.DispatchCompute(shader, kernel, groupsX, groupsY, 1);
                    m_IdTarget.Swap();
                    m_CoverageTarget.Swap();
                }

                cmd.RequestAsyncReadback(m_IdTarget.In, OnIdTargetReadback);
            }
        }

        /// <summary>
        /// Returns the byte id corresponding to the passed normalized coordinates.
        /// </summary>
        /// <param name="normalizedCoordinates">The normalized coordinates for which we query the id.</param>
        /// <returns>The id at the coordinates.</returns>
        /// <exception cref="InvalidOperationException">Raised if picking is disabled.</exception>
        public byte GetIdAtCoordinates(Vector2 normalizedCoordinates)
        {
            if (!m_PickingEnabled)
            {
                throw new InvalidOperationException(
                    $"Trying to fetch picking Id while {nameof(m_PickingEnabled)} is set to false." +
                    $"Activate picking by invoking {nameof(SetPickingEnabled)}.");
            }

            if (!m_IdBuffer.IsCreated || m_ReadbackSize.x * m_ReadbackSize.y != m_IdBuffer.Length)
            {
                return 0;
            }

            var coords = math.saturate(normalizedCoordinates) *
                new float2(m_ReadbackSize.x - 1, m_ReadbackSize.y - 1);
            var index = (int)coords.y * m_ReadbackSize.x + (int)coords.x;
            return m_IdBuffer[index];
        }

        void OnIdTargetReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlotRenderer)}, Id target readback failed.");
            }

            var data = req.GetData<byte>();
            if (data.Length != m_ReadbackSize.x * m_ReadbackSize.y)
            {
                // Delay between an async readback request and a new request at
                // a difference size will happen when resizing the plot renderer in this case
                // we ignore the outdated old request
                return;
            }

            Utilities.AllocateNativeArrayIfNeeded(ref m_IdBuffer, data.Length);
            m_IdBuffer.CopyFrom(data);
        }
    }
}
