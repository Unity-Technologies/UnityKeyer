Shader "Hidden/Keyer/Plot"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentTextureRgb
                #include "Plot.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentTextureRgb
                #define DRAW_FILLED
                #include "Plot.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentTextureSingleChannel
                #include "Plot.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentTextureSingleChannel
                #define DRAW_FILLED
                #include "Plot.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentBuffer
                #include "Plot.hlsl"
            ENDHLSL
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Blend One One
            Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentBuffer
                #define DRAW_FILLED
                #include "Plot.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
