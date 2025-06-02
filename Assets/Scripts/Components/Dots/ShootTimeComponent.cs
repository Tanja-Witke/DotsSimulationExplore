using Unity.Entities;

public struct ShootTimeComponent : IComponentData
{
    public double NextAllowedShootTime; // World time when next dot can spawn
}
