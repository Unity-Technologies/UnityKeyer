Texture2D _SourceTexture;
SamplerState sampler_LinearClamp;
float4 _SourceScaleBias;
float4 _TargetScaleBias;
float _SourceMipLevel;
float4 _SourceTexelSize;

struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

Varyings Vertex(Attributes input)
{
    Varyings output;
    output.positionCS = GetQuadVertexPosition(input.vertexID) * float4(_TargetScaleBias.x, _TargetScaleBias.y, 1, 1)
        + float4(_TargetScaleBias.z, _TargetScaleBias.w, 0, 0);
    output.positionCS.xy = output.positionCS.xy * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f); //convert to -1..1
    #if defined(FLIP_GEOMETRY_VERTICAL)
    output.positionCS.y *= -1;
    #endif
    output.texcoord = GetQuadTexCoord(input.vertexID) * _SourceScaleBias.xy + _SourceScaleBias.zw;
    return output;
}
