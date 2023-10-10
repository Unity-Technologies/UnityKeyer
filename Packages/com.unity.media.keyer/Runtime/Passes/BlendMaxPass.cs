using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct BlendMaxPassData
    {
        public TextureHandle Foreground;
        public TextureHandle Background;
        public TextureHandle Output;
        public float Amount;
    }

    class BlendMaxPass : IRenderPass
    {
        readonly BlendMaxPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get
            {
                yield return m_Data.Foreground;
                yield return m_Data.Background;
            }
        }

        public BlendMaxPass(BlendMaxPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var foreground = (Texture)m_Data.Foreground;
            var background = (Texture)m_Data.Background;
            var output = (Texture)m_Data.Output;

            Assert.IsTrue(foreground.width == background.width);
            Assert.IsTrue(foreground.height == background.height);

            var cs = ctx.Shaders.BlendMax;
            var kernel = ctx.KernelIds.BlendMax;

            cmd.SetComputeFloatParam(cs, ShaderIDs._Amount, m_Data.Amount);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Foreground, foreground);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Background, background);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
