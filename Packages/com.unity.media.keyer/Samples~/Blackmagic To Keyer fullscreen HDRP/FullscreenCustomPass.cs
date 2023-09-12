using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// This class is drawing the Input Video texture through a Custom Pass.
    /// The texture is drawn in fullscreen mode.
    /// </summary>
    [Serializable]
    public sealed class FullScreenCustomPass : CustomPass
    {
        class ShaderIDs
        {
            public const string _BackgroundParameterID = "_Background";
            public const string _BlitVideoPassID = "_BlitTexture";
        }

        [SerializeField]
        Material m_Material;

        [SerializeField]
        RenderTexture m_RenderTexture;

        int m_VideoFullscreenPass;
        int m_BackGroundId;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            m_BackGroundId = Shader.PropertyToID(ShaderIDs._BackgroundParameterID);
            m_VideoFullscreenPass = m_Material.FindPass(ShaderIDs._BlitVideoPassID);
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (!m_RenderTexture || !m_Material)
                return;

            m_Material.SetTexture(m_BackGroundId, m_RenderTexture);
            CoreUtils.DrawFullScreen(ctx.cmd, m_Material, shaderPassId: m_VideoFullscreenPass);
        }
    }
}
