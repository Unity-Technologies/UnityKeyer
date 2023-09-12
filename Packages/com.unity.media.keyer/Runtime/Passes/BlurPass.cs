using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct BlurPassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public TextureHandle Temp;
        public float Radius;
        public int SampleCount;
    }

    class BlurPass : IRenderPass
    {
        readonly BlurPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get
            {
                yield return m_Data.Input;
                yield return m_Data.Temp;
            }
        }

        public BlurPass(BlurPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            if (m_Data.SampleCount < 1)
            {
                throw new ArgumentException(
                    $"{nameof(m_Data.SampleCount)} must be superior or equal to 1.");
            }

            var input = (Texture)m_Data.Input;
            var output = (Texture)m_Data.Output;
            var temp = (Texture)m_Data.Temp;

            var cs = ctx.Shaders.Blur;
            var kernelHorizontal = ctx.KernelIds.BlurHorizontal;
            var kernelVertical = ctx.KernelIds.BlurVertical;
            // TODO Should we manage that buffer here, explicitly.
            var gaussianWeights = GaussianWeights.Get(m_Data.SampleCount);

            Utilities.SetTexelSize(cmd, cs, input);

            cmd.SetComputeIntParam(cs, ShaderIDs._SampleCount, m_Data.SampleCount);
            cmd.SetComputeFloatParam(cs, ShaderIDs._Radius, m_Data.Radius);

            cmd.SetComputeBufferParam(cs, kernelHorizontal, ShaderIDs._BlurWeights, gaussianWeights);
            cmd.SetComputeTextureParam(cs, kernelHorizontal, ShaderIDs._Input, input);
            cmd.SetComputeTextureParam(cs, kernelHorizontal, ShaderIDs._Output, temp);
            Utilities.DispatchCompute(cmd, cs, kernelHorizontal, temp.width, temp.height);

            cmd.SetComputeBufferParam(cs, kernelVertical, ShaderIDs._BlurWeights, gaussianWeights);
            cmd.SetComputeTextureParam(cs, kernelVertical, ShaderIDs._Input, temp);
            cmd.SetComputeTextureParam(cs, kernelVertical, ShaderIDs._Output, output);
            Utilities.DispatchCompute(cmd, cs, kernelVertical, output.width, output.height);
        }
    }
}
