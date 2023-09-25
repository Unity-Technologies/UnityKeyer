using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SimpleCompositingCustomPass : CustomPass
{
    class ShaderIDs
    {
        public const string _BackgroundParameterID = "_Background";
        public const string _FadeValueID = "_FadeValue";
        public const string _BlitVideoPassID = "_BlitTexture";
        public const string _CustomBufferID = "_CustomBuffer";
        public const string _OverLayerID = "_OverLayer";
        public const string _CompositingPass = "Compositing";
        public const string _CopyPass = "Copy";
    }

    [SerializeField]
    RenderTexture m_RenderTexture;

    [SerializeField]
    Material compositingMaterial;

    [SerializeField]
    bool fetchColorBuffer;

    // Manual compositing
    RTHandle customBuffer;
    int compositingPass;
    int copyPass;
    int fadeValueId;
    int backGroundId;

    protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
    {
        // Manual Compositing
        fadeValueId = Shader.PropertyToID(ShaderIDs._FadeValueID);
        backGroundId = Shader.PropertyToID(ShaderIDs._BackgroundParameterID);

        customBuffer = RTHandles.Alloc(Vector2.one, TextureXR.slices, dimension: TextureXR.dimension,
                                       colorFormat: GraphicsFormat.R16G16B16A16_SFloat, useDynamicScale: true,
                                       name: "Custom Compositing Buffer");

        compositingPass = compositingMaterial.FindPass(ShaderIDs._CompositingPass);
        copyPass = compositingMaterial.FindPass(ShaderIDs._CopyPass);
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (!m_RenderTexture || !compositingMaterial)
            return;

        if (fetchColorBuffer)
        {
            ResolveMSAAColorBuffer(ctx.cmd, ctx.hdCamera);
            SetRenderTargetAuto(ctx.cmd);
        }

        compositingMaterial.SetTexture(ShaderIDs._CustomBufferID, customBuffer);
        compositingMaterial.SetTexture(ShaderIDs._OverLayerID, m_RenderTexture);

        compositingMaterial.SetTexture(backGroundId, ctx.cameraColorBuffer);
        compositingMaterial.SetFloat(fadeValueId, fadeValue);

        CoreUtils.SetRenderTarget(ctx.cmd, customBuffer, ClearFlag.All);
        CoreUtils.DrawFullScreen(ctx.cmd, compositingMaterial, shaderPassId: compositingPass);

        SetRenderTargetAuto(ctx.cmd);
        CoreUtils.DrawFullScreen(ctx.cmd, compositingMaterial, shaderPassId: copyPass);
    }

    public override IEnumerable<Material> RegisterMaterialForInspector() { yield return compositingMaterial; }

    protected override void Cleanup()
    {
        customBuffer.Release();
    }
}
