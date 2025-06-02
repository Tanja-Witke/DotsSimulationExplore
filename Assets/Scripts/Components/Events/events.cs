using Unity.Entities;

internal struct CollisionEventFlag : IComponentData {}
internal struct ImpactEventFlag : IComponentData {}
internal struct SpawnRequestEventFlag : IComponentData {}
internal struct GhostRequestEventFlag : IComponentData {}
internal struct KillRequestEventFlag : IComponentData {}