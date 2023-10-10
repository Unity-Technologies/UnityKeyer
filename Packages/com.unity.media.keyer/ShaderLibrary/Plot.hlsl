#include "Common.hlsl"
#include "VertexFullScreen.hlsl"

SamplerState sampler_BilinearClamp;
StructuredBuffer<float> _SourceBuffer;
float4 _TargetTexelSize;
float3 _Channels;
float3 _Color;
float4 _PlotParams;
float4 _Line;
#define _Thickness _PlotParams.x
#define _Smoothness _PlotParams.y
#define _Opacity _PlotParams.z
#define _NumSamples _PlotParams.w

#define EPSILON 1e-4

inline float LinearStep(float a, float b, float x)
{
    return saturate((x - a) / (b - a));
}

inline float DrawFilled(float v, float2 thickness, float2 edge, float fillOpacity)
{
    float x = LinearStep(thickness.x + edge.x, thickness.x, v);
    float y = LinearStep(thickness.y + edge.y, thickness.y, v);
    return min(x, y) + y * fillOpacity;
}

inline float DrawLine(float v, float2 thickness, float2 edge, float fillOpacity)
{
    return DrawFilled(v, thickness, edge, 0);
}

#ifdef DRAW_FILLED
#define DRAW_CURVE DrawFilled
#else
#define DRAW_CURVE DrawLine
#endif

float SmoothCurve(float value, float threshold, float2 slope)
{
    // From derivatives to "plot slice extent".
    // Note that we flip the left derivative.
    const float2 span = float2(
        min(-EPSILON, min(slope.x * -1, slope.y)),
        max(EPSILON, max(slope.x * -1, slope.y)));

    const float2 thickness = span * (1 - _Smoothness) + float2(-0.5, 0.5) * _TargetTexelSize.w * _Thickness;
    float2 edge = span * _Smoothness * 2;

    // Take in account pixel vertical snapping for additional anti-aliasing.
    const float pixelSnap = abs(frac(value * _TargetTexelSize.y) - 0.5) * 2; // [0, 1] Range.

    // Apply the effect of pixel snapping to edges.
    const float pixelSnapToEdge = lerp(1, 0.2, pixelSnap) * _TargetTexelSize.w * _Smoothness;
    edge = float2(min(edge.x, -pixelSnapToEdge), max(edge.y, pixelSnapToEdge));

    return DRAW_CURVE(threshold - value, thickness, edge, _Opacity);
}

float3 SmoothCurveRGB(float3 color, float y, float3 dyL, float3 dyR)
{
    return float3(
        SmoothCurve(color.r, y, float2(dyL.r, dyR.r)),
        SmoothCurve(color.g, y, float2(dyL.g, dyR.g)),
        SmoothCurve(color.b, y, float2(dyL.b, dyR.b)));
}

float3 SampleSourceTexture(float x, int indexOffset = 0)
{
    int index = floor(x * _TargetTexelSize.x);
    index = clamp(0, _NumSamples - 1, index + indexOffset);
    const float2 uv = lerp(_Line.xy, _Line.zw, index / _NumSamples);
    return _SourceTexture.SampleLevel(sampler_BilinearClamp, uv, 0).rgb;
}

float SampleSourceBuffer(float x, int indexOffset = 0)
{
    int index = floor(x * _TargetTexelSize.x);
    return _SourceBuffer[clamp(0, _NumSamples - 1, index + indexOffset)];
}

float4 FragmentTextureRgb(Varyings input) : SV_Target
{
    float3 sampleL = SampleSourceTexture(input.texcoord.x, -1);
    float3 sampleC = SampleSourceTexture(input.texcoord.x);
    float3 sampleR = SampleSourceTexture(input.texcoord.x, 1);

    // Finite differences.
    float3 deltaL = (sampleC - sampleL) * 0.5;
    float3 deltaR = (sampleR - sampleC) * 0.5;

    float3 curve = SmoothCurveRGB(sampleC, input.texcoord.y, deltaL, deltaR) * _Channels;

    if (dot(curve, float3(1, 1, 1)) < EPSILON)
    {
        discard;
    }

    return float4(curve, 1);
}

float4 FragmentTextureSingleChannel(Varyings input) : SV_Target
{
    float sampleL = SampleSourceTexture(input.texcoord.x, -1).r;
    float sampleC = SampleSourceTexture(input.texcoord.x).r;
    float sampleR = SampleSourceTexture(input.texcoord.x, 1).r;

    // Finite differences.
    float deltaL = (sampleC - sampleL) * 0.5;
    float deltaR = (sampleR - sampleC) * 0.5;

    const float curve = SmoothCurve(sampleC, input.texcoord.y, float2(deltaL, deltaR));

    if (curve < EPSILON)
    {
        discard;
    }

    return float4(curve * _Color, 1);
}

float4 FragmentBuffer(Varyings input) : SV_Target
{
    float sampleL = SampleSourceBuffer(input.texcoord.x, -1);
    float sampleC = SampleSourceBuffer(input.texcoord.x);
    float sampleR = SampleSourceBuffer(input.texcoord.x, 1);

    // Finite differences.
    float deltaL = (sampleC - sampleL) * 0.5;
    float deltaR = (sampleR - sampleC) * 0.5;

    const float curve = SmoothCurve(sampleC, input.texcoord.y, float2(deltaL, deltaR));

    if (curve < EPSILON)
    {
        discard;
    }

    return float4(curve * _Color, 1);
}
