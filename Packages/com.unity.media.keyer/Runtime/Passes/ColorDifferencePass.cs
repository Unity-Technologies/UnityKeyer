using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct ColorDifferencePassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public BackgroundChannel BackgroundChannel;
        public Vector3 Scale;
        public Vector2 Clip;
    }

    class ColorDifferencePass : IRenderPass
    {
        readonly ColorDifferencePassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get { yield return m_Data.Input; }
        }

        public ColorDifferencePass(ColorDifferencePassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.ColorDifference;
            var kernel = m_Data.BackgroundChannel == BackgroundChannel.Green ?
                ctx.KernelIds.ColorDifferenceGreen : ctx.KernelIds.ColorDifferenceBlue;

            cmd.SetComputeVectorParam(cs, ShaderIDs._Scale, m_Data.Scale);
            cmd.SetComputeVectorParam(cs, ShaderIDs._Clip, m_Data.Clip);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Input, (Texture)m_Data.Input);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
