using Unity.Entities;
using Unity.Mathematics;

public struct DotInitComponent : IComponentData
{
    public int LevelNumber;
    public Strategy Strategy;
    public float3 Position;
    public float4 Color;
    public float3 Direction;
    public Entity DotEntity;
}