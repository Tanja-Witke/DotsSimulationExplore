using Unity.Entities;
using Unity.Physics.Authoring;

public struct GhostPrefabComponent : IComponentData
{
    public Entity Prefab;
}
