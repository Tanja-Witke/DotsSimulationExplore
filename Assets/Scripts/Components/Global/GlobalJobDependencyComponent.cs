using Unity.Entities;
using Unity.Jobs;

public struct GlobalJobDependencyComponent : IComponentData
{
    public JobHandle CombinedHandle;
}
