// Constant buffers macros.
// Note that the exception is OpenGL ES 2.0.
#ifdef SHADER_API_GLES
    #define CBUFFER_START(name)
    #define CBUFF
#else
    #define CBUFFER_START(name) cbuffer name {
    #define CBUFFER_END };
#endif

// TODO Is it wise to include this everywhere we do?
// Can we trust it being stripped by the shder processor/compiler if not used?
CBUFFER_START(cb0)
    float4 _TexelSize; // xy: size, zw: texel size
CBUFFER_END

SamplerState _LinearClamp;

#define SAMPLE(tex, xy) (tex[xy])
#define SAMPLE_RGBA(tex, xy) (SAMPLE(tex, xy).rgba)
#define SAMPLE_RGB(tex, xy) (SAMPLE(tex, xy).rgb)
#define SAMPLE_R(tex, xy) (SAMPLE(tex, xy).r)

#define SAMPLE_LINEAR(tex, xy) (tex.SampleLevel(_LinearClamp, xy * _TexelSize.zw, 0))
#define SAMPLE_LINEAR_RGBA(tex, xy) (SAMPLE_LINEAR(tex, xy).rgba)
#define SAMPLE_LINEAR_RGB(tex, xy) (SAMPLE_LINEAR(tex, xy).rgb)
#define SAMPLE_LINEAR_R(tex, xy) (SAMPLE_LINEAR(tex, xy).r)

float InvLerp(float a, float b, float v)
{
    return (v - a) / (b - a);
}

float3 InvLerp(float3 a, float3 b, float3 v)
{
    return (v - a) / (b - a);
}

// Faster than distance and just as good to compare.
float SquareDistance(float3 A, float3 B)
{
    float3 diff = A - B;
    return dot(diff, diff);
}

uint To1DIndex(uint3 id, uint dimension)
{
    return (id.z * dimension * dimension) + (id.y * dimension) + id.x;
}

uint3 To3DIndex(uint id, uint dimension)
{
    uint z = id / (dimension * dimension);
    id -= z * dimension * dimension;

    uint y = id / dimension;
    id -= y * dimension;

    uint x = id / 1;
    return uint3(x, y, z);
}

float3x3 ReadMatrix3x3(RWStructuredBuffer<float3> buffer, uint index)
{
    float3x3 m;
    m[0] = buffer[index * 3];
    m[1] = buffer[index * 3 + 1];
    m[2] = buffer[index * 3 + 2];
    return m;
}

void WriteMatrix3x3(RWStructuredBuffer<float3> buffer, uint index, float3x3 m)
{
    buffer[index * 3] = m[0];
    buffer[index * 3 + 1] = m[1];
    buffer[index * 3 + 2] = m[2];
}

// We often work with symmetric matrices and take advantage of it.
// We can use 2/3rds of "normal" memory. A 3x3 matrix can be encoded in 2 float3s.
// We need a convention regarding the encoding:
// float3(m[0, 0], m[0, 1], m[0, 2]) <- first row, nothing special
// float3(m[1, 1], m[1, 2], m[2, 2]) <- the tricky part
float3x3 ReadMatrixSymmetric3x3(RWStructuredBuffer<float3> buffer, uint index)
{
    float3 r0 = buffer[index * 2];
    float3 r1 = buffer[index * 2 + 1];

    float3x3 m;
    m[0] = r0;
    m[1] = float3(r0.y, r1.x, r1.y);
    m[2] = float3(r0.z, r1.y, r1.z);

    return m;
}

void WriteMatrixSymmetric3x3(RWStructuredBuffer<float3> buffer, uint index, float3x3 m)
{
    buffer[index * 2] = m[0];
    buffer[index * 2 + 1] = float3(m[1][1], m[1][2], m[2][2]);
}

// Similar to what is provided in "UnityCG.cginc".
float3 LinearToGammaSpace(float3 linRGB)
{
    linRGB = max(linRGB, float3(0, 0, 0));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055 * pow(linRGB, 0.416666667) - 0.055, 0);
}

float3 Rgb2Yuv(float3 rgb)
{
    float4x4 Rgb2YuvTransform =
    {
         0.182586,  0.614231,  0.062007, 0.062745,
        -0.100644, -0.338572,  0.439216, 0.501961,
         0.439216, -0.398942, -0.040274, 0.501961,
         0.000000,  0.000000,  0.000000, 1.000000
    };
    return mul(Rgb2YuvTransform, float4(rgb, 1)).rgb;
}

// returns 1 if v inside the box, returns 0 otherwise
float InsideRect(float2 v, float2 bottomLeft, float2 topRight)
{
    // BottomLeft represents the smallest {x, y} while TopRight the highest, bottomLeft < topRight.
    // step(bottomLeft, v) returns {1,1} if v >= bottomLeft, that is, the point is within the rect {bottomLeft, +infinity},
    // step(topRight, v) returns {0,0} if v < topRight, that is, the point is within the rect {-infinity, topRight},
    // so if the point is within {bottomLeft, topRight}, the intersection of {bottomLeft, +infinity} and {-infinity, topRight},
    // step(bottomLeft, v) returns {1, 1} and step(topRight, v), {0, 0}, so s = {1, 1}.
    // In that case s.x * s.y = 1 * 1 = 1.
    float2 s = step(bottomLeft, v) - step(topRight, v);
    return s.x * s.y;
}
