using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct CombineColorAndAlphaPassData
    {
        public TextureHandle Color;
        public TextureHandle Alpha;
        public TextureHandle Output;
    }

    class CombineColorAndAlphaPass : IRenderPass
    {
        readonly CombineColorAndAlphaPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get
            {
                yield return m_Data.Color;
                yield return m_Data.Alpha;
            }
        }

        public CombineColorAndAlphaPass(CombineColorAndAlphaPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.Combine;
            var kernel = ctx.KernelIds.Combine;

            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Color, (Texture)m_Data.Color);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Alpha, (Texture)m_Data.Alpha);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
