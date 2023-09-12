using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct DespillPassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public BackgroundChannel BackgroundChannel;
        public float Amount;
    }

    class DespillPass : IRenderPass
    {
        readonly DespillPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get { yield return m_Data.Input; }
        }

        public DespillPass(DespillPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.Despill;
            var kernel = m_Data.BackgroundChannel == BackgroundChannel.Green ?
                ctx.KernelIds.DespillGreen : ctx.KernelIds.DespillBlue;

            cmd.SetComputeFloatParam(cs, ShaderIDs._Amount, m_Data.Amount);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Input, (Texture)m_Data.Input);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
