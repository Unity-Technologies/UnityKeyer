Shader "Hidden/Keyer/GarbageMask2d"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "GarbageMask2d.hlsl"
            ENDHLSL
        }

		Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentSdf
                #include "GarbageMask2d.hlsl"
            ENDHLSL
        }

        // Used for SDF demo.
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment FragmentSdfMaskOnly
                #include "GarbageMask2d.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
