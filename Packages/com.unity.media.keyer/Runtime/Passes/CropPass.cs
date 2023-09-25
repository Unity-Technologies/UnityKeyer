using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct CropPassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
        public Rect Rect;
    }

    class CropPass : IRenderPass
    {
        readonly CropPassData m_Data;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get { yield return m_Data.Input; }
        }

        public CropPass(CropPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            var input = (Texture)m_Data.Input;
            var output = (Texture)m_Data.Output;

            var cs = ctx.Shaders.Crop;
            var kernel = ctx.KernelIds.Crop;

            // Note the inversion of the axes. May not be needed if our pipeline changes. Tbd.
            // We also transform the rect to texture space to save the conversion in the shader.
            // Contains (BottomLeft:float2, TopRight:float2).
            var w = input.width;
            var h = input.height;
            cmd.SetComputeVectorParam(cs, ShaderIDs._Rect,
                new Vector4(
                    m_Data.Rect.xMin * w,
                    m_Data.Rect.yMin * h,
                    m_Data.Rect.xMax * w,
                    m_Data.Rect.yMax * h));
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Input, input);
            cmd.SetComputeTextureParam(cs, kernel, ShaderIDs._Output, output);

            Utilities.DispatchCompute(cmd, cs, kernel, output.width, output.height);
        }
    }
}
