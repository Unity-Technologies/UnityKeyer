using System;
using UnityEngine;

namespace Unity.Media.Keyer.PlotRendererDemo
{
    [ExecuteAlways]
    public class PlotRendererDemo : MonoBehaviour
    {
        static class ShaderIDs
        {
            public static readonly int _Color = Shader.PropertyToID("_Color");
            public static readonly int _Line = Shader.PropertyToID("_Line");
        }

        struct Sigmoid : PlotRenderer.IFunction
        {
            public float RangeMin;
            public float RangeMax;

            public float Evaluate(float t)
            {
                t = RangeMin + (RangeMax - RangeMin) * t;

                return 1 / (1 + Mathf.Exp(-t));
            }
        }

        enum PlotId : byte
        {
            None = 0,
            Rgb = 1,
            SingleChannel = 2,
            Function = 3
        }

        [Serializable]
        struct RgbPlotData
        {
            public bool Enabled;
            public Texture Source;
            public PlotRenderer.Channels Channels;
            public PlotRenderer.CurveStyle CurveStyle;
        }

        [Serializable]
        struct SingleChannelPlotData
        {
            public bool Enabled;
            public Texture Source;
            public Color Color;
            public PlotRenderer.CurveStyle CurveStyle;
        }

        [Serializable]
        struct SigmoidPlotData
        {
            public bool Enabled;
            [SerializeField, Range(-20, 0)]
            public float RangeMin;
            [SerializeField, Range(0, 20)]
            public float RangeMax;
            public Color Color;
            public PlotRenderer.CurveStyle CurveStyle;
        }

        [SerializeField]
        RgbPlotData m_RgbPlotData;

        [SerializeField]
        SingleChannelPlotData m_SingleChannelPlotData;

        [SerializeField]
        SigmoidPlotData m_SigmoidPlotData;

        [SerializeField]
        Vector2 m_StartPoint;

        [SerializeField]
        Vector2 m_EndPoint;

        [SerializeField]
        Vector2Int m_Resolution;

        [SerializeField]
        Vector2Int m_GuiResolution;

        [SerializeField]
        Color m_GuiColor;

        [SerializeField]
        bool m_EnablePicking;

        readonly PlotRenderer m_PlotRenderer = new();

        PlotRenderer.IRgbPlot m_RgbPlot;
        PlotRenderer.ISingleChannelPlot m_SingleChannelPlot;
        PlotRenderer.IFunctionPlot<Sigmoid> m_FunctionPlot;

        RenderTexture m_Target;
        RenderTexture m_GuiTarget;

        Material m_GuiMaterial;
        MaterialPropertyBlock m_GuiPropertyBlock;

        PlotId m_LastPlotHovered;

        bool m_Initialized;
        bool m_PendingCapture;

        [ContextMenu("Schedule Capture")]
        void ScheduleCapture() => m_PendingCapture = true;

        void OnGUI()
        {
            var x = 0;

            if (m_GuiTarget != null)
            {
                var rect = new Rect(0, 0, m_GuiResolution.x, m_GuiResolution.y);
                GUI.DrawTexture(rect, m_GuiTarget);
                x = m_GuiResolution.x;
            }

            if (m_Target != null)
            {
                var rect = new Rect(x, 0, m_Resolution.x, m_Resolution.y);
                GUI.DrawTexture(rect, m_Target);

                // Curve picking.
                // Note that we use normalized coordinates.
                var evt = Event.current;
                if (m_EnablePicking && evt != null &&
                    rect.Contains(evt.mousePosition))
                {
                    var coords = evt.mousePosition - rect.position;
                    var normalizedCoords = new Vector2(coords.x / rect.width, (rect.height - coords.y) / rect.height);
                    m_LastPlotHovered = (PlotId)m_PlotRenderer.GetIdAtCoordinates(normalizedCoords);
                }
            }

            if (m_EnablePicking && m_LastPlotHovered != PlotId.None)
            {
                GUI.Label(new Rect(12, 12, 256, 32), $"Last hovered {m_LastPlotHovered}");
            }
        }

        void OnEnable()
        {
            m_GuiMaterial = Utilities.CreateMaterial("Hidden/Keyer/ProceduralLine");

            if (m_GuiPropertyBlock == null)
            {
                m_GuiPropertyBlock = new MaterialPropertyBlock();
            }

            m_PlotRenderer.Initialize();
            m_PlotRenderer.SetPickingEnabled(m_EnablePicking);

            m_RgbPlot = m_PlotRenderer.CreateRgbPlot();
            m_RgbPlot.Id = (byte)PlotId.Rgb;
            m_SingleChannelPlot = m_PlotRenderer.CreateSingleChannelPlot();
            m_SingleChannelPlot.Id = (byte)PlotId.SingleChannel;
            m_FunctionPlot = m_PlotRenderer.CreateFunctionPlot<Sigmoid>();
            m_FunctionPlot.Id = (byte)PlotId.Function;
            m_Initialized = true;

            Render();
        }

        void OnDisable()
        {
            m_Initialized = false;

            m_PlotRenderer.Dispose();
            Utilities.DeallocateIfNeeded(ref m_Target);
            Utilities.DeallocateIfNeeded(ref m_GuiTarget);
            Utilities.Destroy(m_GuiMaterial);
        }

        void OnValidate()
        {
            m_PlotRenderer.SetPickingEnabled(m_EnablePicking);

            if (m_Initialized)
            {
                Render();
            }
        }

        void Render()
        {
            // Validate data.
            m_Resolution.x = Mathf.Max(128, m_Resolution.x);
            m_Resolution.y = Mathf.Max(128, m_Resolution.y);
            m_GuiResolution.x = Mathf.Max(128, m_GuiResolution.x);
            m_GuiResolution.y = Mathf.Max(128, m_GuiResolution.y);

            m_StartPoint.x = Mathf.Clamp01(m_StartPoint.x);
            m_StartPoint.y = Mathf.Clamp01(m_StartPoint.y);
            m_EndPoint.x = Mathf.Clamp01(m_EndPoint.x);
            m_EndPoint.y = Mathf.Clamp01(m_EndPoint.y);

            // Synchronize data.
            m_RgbPlot.Enabled = m_RgbPlotData.Enabled;
            m_RgbPlot.Source = m_RgbPlotData.Source;
            m_RgbPlot.StartPoint = m_StartPoint;
            m_RgbPlot.EndPoint = m_EndPoint;
            m_RgbPlot.Channels = m_RgbPlotData.Channels;
            m_RgbPlot.CurveStyle = m_RgbPlotData.CurveStyle;

            m_SingleChannelPlot.Enabled = m_SingleChannelPlotData.Enabled;
            m_SingleChannelPlot.Source = m_SingleChannelPlotData.Source;
            m_SingleChannelPlot.StartPoint = m_StartPoint;
            m_SingleChannelPlot.EndPoint = m_EndPoint;
            m_SingleChannelPlot.Color = m_SingleChannelPlotData.Color;
            m_SingleChannelPlot.CurveStyle = m_SingleChannelPlotData.CurveStyle;

            m_FunctionPlot.Enabled = m_SigmoidPlotData.Enabled;
            m_FunctionPlot.Function = new Sigmoid
            {
                RangeMin = m_SigmoidPlotData.RangeMin,
                RangeMax = m_SigmoidPlotData.RangeMax
            };
            m_FunctionPlot.Color = m_SigmoidPlotData.Color;
            m_FunctionPlot.CurveStyle = m_SigmoidPlotData.CurveStyle;

            var cmd = CommandBufferPool.Get("Plot");

            // Render GUI.
            if (m_RgbPlot.Source != null)
            {
                m_GuiPropertyBlock.SetColor(ShaderIDs._Color, m_GuiColor);

                // Note the flip of the Y axis.
                m_GuiPropertyBlock.SetVector(ShaderIDs._Line,
                    new Vector4(m_StartPoint.x, 1 - m_StartPoint.y, m_EndPoint.x, 1 - m_EndPoint.y));

                Utilities.AllocateIfNeeded(ref m_GuiTarget, m_GuiResolution.x, m_GuiResolution.y);

                cmd.SetRenderTarget(m_GuiTarget);

                Blitter.Blit(cmd, m_RgbPlot.Source, 0);

                cmd.DrawProcedural(Matrix4x4.identity, m_GuiMaterial, 0, MeshTopology.Lines, 2, 1, m_GuiPropertyBlock);
            }

            Utilities.AllocateIfNeeded(ref m_Target, m_Resolution.x, m_Resolution.y);

            m_PlotRenderer.Render(cmd, m_Target, m_Resolution.x);

            if (m_PendingCapture)
            {
                using var scope = EditorBridge.CreateRenderDocCaptureScope();
                Graphics.ExecuteCommandBuffer(cmd);
                m_PendingCapture = false;
            }
            else
            {
                Graphics.ExecuteCommandBuffer(cmd);
            }

            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}
