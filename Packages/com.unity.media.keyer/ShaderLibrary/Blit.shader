Shader "Hidden/Keyer/Blit"
{
    SubShader
    {
        // Default.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "Blit.hlsl"
            ENDHLSL
        }

        // Flip Y.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #define FLIP_GEOMETRY_VERTICAL
                #include "Blit.hlsl"
            ENDHLSL
        }

        // Opaque.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentOpaque
                #include "Blit.hlsl"
            ENDHLSL
        }

        // Single Channel.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentSingleChannel
                #include "Blit.hlsl"
            ENDHLSL
        }

        // Additive.
        Pass
        {
            ZWrite Off ZTest Always Blend One One Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "Blit.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
