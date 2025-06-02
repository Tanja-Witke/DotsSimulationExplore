using Unity.Entities;
using Unity.Mathematics;

[InternalBufferCapacity(100)]
public struct InitShootedGhostBufferElement : IBufferElementData
{
    public int LevelIndex;
    public Strategy Strategy;
    public float3 Position;
    public float4 Color;
    public float3 Direction;
}