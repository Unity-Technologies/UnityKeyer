#include "Common.hlsl"
#include "VertexFullScreen.hlsl"

float4 Fragment(Varyings input) : SV_Target
{
    return _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel);
}

float4 FragmentOpaque(Varyings input) : SV_Target
{
    float3 color = _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel).rgb;
    return float4(color, 1);
}

float4 FragmentSingleChannel(Varyings input) : SV_Target
{
    float color = _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel).r;
    return float4(color, color, color, 1);
}
