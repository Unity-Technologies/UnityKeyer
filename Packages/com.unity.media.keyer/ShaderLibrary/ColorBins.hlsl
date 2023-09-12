// A snippet for color bin evaluation.ColorBins
// Requires CommonCompute.hlsl to be included.
#ifdef COLORBINS_BOUNDS
float3 _BoundsMin;
float3 _BoundsMax;
#endif

RWStructuredBuffer<float3> _Samples;
RWStructuredBuffer<uint> _ColorBins;

[numthreads(GROUP_SIZE, 1 ,1)]
void ResetColorBins(uint3 id : SV_DispatchThreadID)
{
    _ColorBins[id.x] = 0;
}

[numthreads(GROUP_SIZE, 1 ,1)]
void UpdateColorBins(uint3 id : SV_DispatchThreadID)
{
    float3 sample = _Samples[id.x];
    // Assign sample to bin.
    // First normalize within bounds.
    // clamp is here for safety in case the bounding box is not perfect,
    // and to prevent out-of-range indices for samples lying on the bounding box faces.
#ifdef COLORBINS_BOUNDS
    float3 normalized = clamp(InvLerp(_BoundsMin, _BoundsMax, sample), 0, 0.99);
#else
    float3 normalized = clamp(sample, 0, 0.99);
#endif
    // implicit floor.
    uint3 index3d = normalized * GRID_SIZE;
    uint index = To1DIndex(index3d, GRID_SIZE);
    InterlockedAdd(_ColorBins[index], 1);
}
