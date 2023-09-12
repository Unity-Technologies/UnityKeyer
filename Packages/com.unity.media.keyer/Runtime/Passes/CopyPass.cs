using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct CopyPassData
    {
        public TextureHandle Input;
        public TextureHandle Output;
    }

    class CopyPass : IRenderPass
    {
        readonly CopyPassData m_Data;
        readonly Blitter.Pass m_BlitPass;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get { yield return m_Data.Input; }
        }

        public CopyPass(CopyPassData data)
        {
            m_Data = data;
            var inputFormat = m_Data.Input.GetGraphicsFormat();
            var outputFormat = m_Data.Output.GetGraphicsFormat();
            var inputColorCount = GraphicsFormatUtility.GetColorComponentCount(inputFormat);
            var outputColorCount = GraphicsFormatUtility.GetColorComponentCount(outputFormat);

            m_BlitPass = Blitter.Pass.Default;
            if (inputColorCount == 1 && outputColorCount == 3)
            {
                m_BlitPass = Blitter.Pass.SingleChannel;
            }
            else if (inputColorCount != outputColorCount)
            {
                // For the time being, this should not happen.
                // If it became a legitimate use case,
                // we would add corresponding Blitter passes.
                throw new InvalidOperationException(
                    $"{nameof(CopyPass)}, invalid resources, input has {inputColorCount} color channels, output has {outputColorCount}.");
            }
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            cmd.SetRenderTarget((Texture)m_Data.Output);
            Blitter.Blit(cmd, m_Data.Input, m_BlitPass);
        }
    }
}
