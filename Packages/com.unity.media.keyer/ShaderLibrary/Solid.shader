Shader "Hidden/Keyer/Solid"
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
            #pragma fragment Fragment

            StructuredBuffer<float2> _Vertices;
            StructuredBuffer<int> _Indices;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                int index = _Indices[input.vertexID];
				float2 position = _Vertices[index];
                // To clip space.
				position = (position - (0.5).xx) * 2.0;
				position.y *= -1;
                output.positionCS = float4(position, 0, 1);
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                return (1.0).xxxx;
            }

            ENDHLSL
        }
    }
}
