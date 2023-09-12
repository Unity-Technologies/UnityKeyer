using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct ClipPassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public Vector2 Range;
    }

    class ClipPass : IRenderPass
    {
        readonly ClipPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get
            {
                yield return m_Data.Input;
            }
        }

        public ClipPass(ClipPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.Clip;
            var kernel = ctx.KernelIds.Clip;

            cmd.SetComputeVectorParam(cs, ShaderIDs._Range, m_Data.Range);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Input, (Texture)m_Data.Input);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
