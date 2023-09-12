using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct ColorDistancePassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public Color ChromaKey;
        public Vector2 Threshold;
    }

    class ColorDistancePass : IRenderPass
    {
        readonly ColorDistancePassData m_Data;
        readonly float m_ThresholdScale;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get { yield return m_Data.Input; }
        }

        public ColorDistancePass(ColorDistancePassData data)
        {
            m_Data = data;
            m_ThresholdScale = GetMaxChromaDistanceYuv();
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.ColorDistance;
            var kernel = ctx.KernelIds.ColorDistance;

            // We pass the keying color chromaticity directly,
            // this saves us computations in the shader.
            var keyChroma = ColorUtil.RgbToYuv(m_Data.ChromaKey);

            cmd.SetComputeVectorParam(cs, ShaderIDs._ColorDistanceParams,
                new Vector4(keyChroma.y, keyChroma.z, m_Data.Threshold.x * m_ThresholdScale, m_Data.Threshold.y * m_ThresholdScale));
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Input, (Texture)m_Data.Input);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }

        public void Setup() { }
        public void Dispose() { }

        // We want to evaluate the max possible chroma distance in YUV space.
        static float GetMaxChromaDistanceYuv()
        {
            var red = ((float3)ColorUtil.RgbToYuv(Color.red)).yz;
            var green = ((float3)ColorUtil.RgbToYuv(Color.green)).yz;
            var blue = ((float3)ColorUtil.RgbToYuv(Color.blue)).yz;

            var rg = math.length(red - green);
            var bg = math.length(blue - green);
            var rb = math.length(red - blue);

            return math.max(math.max(rg, bg), rb);
        }
    }
}
