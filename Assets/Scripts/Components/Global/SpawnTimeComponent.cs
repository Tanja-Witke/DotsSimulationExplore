using Unity.Entities;

public struct SpawnTimeComponent : IComponentData
{
    public double NextAllowedTime;
}
