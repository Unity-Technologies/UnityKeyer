using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    struct GarbageMaskPassData
    {
        public TextureHandle Input;
        public TextureHandle Mask;
        public TextureHandle Output;
        public bool SdfEnabled;
        public float SdfDistance;
        public float Threshold;
        public float Blend;
        public float Invert;
    }

    class GarbageMaskPass : IRenderPass
    {
        readonly GarbageMaskPassData m_Data;

        static Material s_Material;

        static Material GetMaterial()
        {
            if (s_Material == null)
            {
                var shader = KeyerResources.GetInstance().Shaders.GarbageMask2d;
                s_Material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return s_Material;
        }

        MaterialPropertyBlock m_PropertyBlock;

        public TextureHandle Output => m_Data.Output;

        public IEnumerable<TextureHandle> Inputs
        {
            get
            {
                yield return m_Data.Input;
                yield return m_Data.Mask;
            }
        }

        public GarbageMaskPass(GarbageMaskPassData data)
        {
            m_Data = data;
        }

        public void Execute(CommandBuffer cmd, Context ctx)
        {
            // TODO Should passes have Init/Dispose?
            if (m_PropertyBlock == null)
            {
                m_PropertyBlock = new MaterialPropertyBlock();
            }

            var input = (Texture)m_Data.Input;
            var mask = (Texture)m_Data.Mask;
            var output = (RenderTexture)m_Data.Output;

            const float epsilon = 1e-2f;
            var threshold = math.clamp(m_Data.Threshold / m_Data.SdfDistance, epsilon * 2, 1 - epsilon);
            var blend = math.clamp(m_Data.Blend / m_Data.SdfDistance, epsilon, threshold - epsilon);

            m_PropertyBlock.SetTexture(ShaderIDs._MaskTexture, mask);
            var garbageMaskParams = new Vector4(threshold, blend, m_Data.Invert);
            m_PropertyBlock.SetVector(ShaderIDs._GarbageMaskParams, garbageMaskParams);

            cmd.SetRenderTarget(output);
            var pass = m_Data.SdfEnabled ? 1 : 0;
            Blitter.Blit(GetMaterial(), m_PropertyBlock, cmd, input, Utilities.IdentityScaleBias, Utilities.IdentityScaleBias, pass);
        }
    }
}
