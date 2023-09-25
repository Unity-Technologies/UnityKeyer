#include "Common.hlsl"
#include "VertexFullScreen.hlsl"

Texture2D _MaskTexture;

float3 _GarbageMaskParams;
#define _Threshold _GarbageMaskParams.x
#define _Blend _GarbageMaskParams.y
#define _Invert _GarbageMaskParams.z

float ApplyInvert(float blend)
{
    return lerp(blend, 1 - blend, _Invert);
}

float GetSdfBlend(float mask)
{
    float opacity = saturate((mask - _Threshold + _Blend) / _Blend);
    return ApplyInvert(opacity);
}

float4 Fragment(Varyings input) : SV_Target
{
    float4 source = _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel);
    float mask = _MaskTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel).r;
    // Use screen space derivative for antialiased edges.
    float delta = max(0.0001, fwidth(mask) * 0.5);
    float blend = ApplyInvert(smoothstep(0.5 - delta, 0.5 + delta, 1 - mask));
    return source * blend;
}

float4 FragmentSdf(Varyings input) : SV_Target
{
    float4 source = _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel);
    float mask = _MaskTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel).r;
    return source * GetSdfBlend(1 - mask);
}

float4 FragmentSdfMaskOnly(Varyings input) : SV_Target
{
    float mask = _SourceTexture.SampleLevel(sampler_LinearClamp, input.texcoord.xy, _SourceMipLevel).r;
    return float4(GetSdfBlend(1 - mask), 0, 0, 1);
}
