Shader "Hidden/BlackmagicVideo/Shader/FullScreenBlitTexture"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    Texture2D _Background;

    float4 BlitVideo(Varyings varyings) : SV_Target
    {
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

        return _Background.Sample(s_point_clamp_sampler, posInput.positionNDC.xy);
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "_BlitTexture"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment BlitVideo
            ENDHLSL
        }
    }
    Fallback Off
}
