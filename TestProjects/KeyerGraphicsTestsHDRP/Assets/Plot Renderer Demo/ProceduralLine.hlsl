struct Attributes
{
    uint vertexID : SV_VertexID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

float4 _Color;
float4 _Line;

// We assume there's at most 2 vertices.
Varyings Vertex(Attributes input)
{
    Varyings output;
    uint id = input.vertexID;
    output.positionCS = float4(_Line[id * 2] * 2 - 1, _Line[id * 2 + 1] * 2 - 1, 0, 1);
    return output;
}

float4 Fragment(Varyings input) : SV_Target
{
    return _Color;
}
