Shader "Hidden/Keyer/ProceduralLine"
{
    SubShader
    {
        Pass
        {
            ZWrite Off ZTest Always Blend Off Cull Off

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Fragment
                #include "ProceduralLine.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
