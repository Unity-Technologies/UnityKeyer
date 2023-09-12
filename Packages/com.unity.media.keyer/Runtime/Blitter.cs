using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    static class Blitter
    {
        public enum Pass : byte
        {
            Default = 0,
            FlipY = 1,
            Opaque = 2,
            SingleChannel = 3,
            Additive = 4
        }

        static Material s_BlitMaterial;
        static MaterialPropertyBlock s_PropertyBlock;

        static MaterialPropertyBlock GetPropertyBlock()
        {
            if (s_PropertyBlock == null)
            {
                s_PropertyBlock = new MaterialPropertyBlock();
            }

            return s_PropertyBlock;
        }

        static Material GetBlitMaterial()
        {
            if (s_BlitMaterial == null)
            {
                var shader = KeyerResources.GetInstance().Shaders.Blit;
                s_BlitMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            return s_BlitMaterial;
        }

        public static void Blit(CommandBuffer cmd, Texture source, Pass pass)
        {
            Blit(cmd, source, Utilities.IdentityScaleBias, Utilities.IdentityScaleBias, (int)pass);
        }

        public static void Blit(CommandBuffer cmd, Texture source, int pass)
        {
            Blit(cmd, source, Utilities.IdentityScaleBias, Utilities.IdentityScaleBias, pass);
        }

        public static void Blit(CommandBuffer cmd, Texture source, Vector4 texBias, Vector4 rtBias, Pass pass)
        {
            Blit(GetBlitMaterial(), GetPropertyBlock(), cmd, source, texBias, rtBias, (int)pass);
        }

        public static void Blit(CommandBuffer cmd, Texture source, Vector4 texBias, Vector4 rtBias, int pass)
        {
            Blit(GetBlitMaterial(), GetPropertyBlock(), cmd, source, texBias, rtBias, pass);
        }

        public static void Blit(Material material, MaterialPropertyBlock propertyBlock, CommandBuffer cmd, Texture source, Vector4 texBias, Vector4 rtBias, int pass)
        {
            propertyBlock.SetTexture(ShaderIDs._SourceTexture, source);
            propertyBlock.SetVector(ShaderIDs._SourceScaleBias, texBias);
            propertyBlock.SetVector(ShaderIDs._TargetScaleBias, rtBias);
            propertyBlock.SetFloat(ShaderIDs._SourceMipLevel, 0);

            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Quads, 4, 1, propertyBlock);
        }
    }
}
