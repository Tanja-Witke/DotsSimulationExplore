using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct InDirectionStrategySystem : ISubSystem
{
    private EntityCommandBuffer _ecb;

    [BurstCompile]
    public partial struct TargetInDirectionJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(
            [EntityIndexInQuery] int index,
            Entity dotEntity,
            [WithChangeFilter] in CollidedComponent changeTarget,
            in TargetStrategyComponent targetStrategy)
        {
            Logic(
                index,
                dotEntity,
                ECB,
                in changeTarget,
                in targetStrategy);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CollidedComponent>();
        state.RequireForUpdate<TargetStrategyComponent>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

        state.RequireForUpdate<CollisionEventFlag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (changeTarget, targetStrategy, dotEntity)
            in SystemAPI.Query<
                RefRO<CollidedComponent>,
                RefRO<TargetStrategyComponent>>()
                .WithEntityAccess()
                .WithChangeFilter<CollidedComponent>())
        {
            Logic(
                0,
                dotEntity,
                _ecb.AsParallelWriter(),
                in changeTarget.ValueRO,
                in targetStrategy.ValueRO);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Logic(
        int index, 
        Entity dotEntity,
        EntityCommandBuffer.ParallelWriter ecb,
        in CollidedComponent changeTarget,
        in TargetStrategyComponent targetStrategy)
    {
        if (targetStrategy.Strategy != Strategy.InDirection || !changeTarget.Collided)
            return;

        ecb.AddComponent(index, dotEntity, new CollidedComponent { Collided = false });
        ecb.AddComponent(index, dotEntity, new TargetStrategyComponent { Strategy = Strategy.Random });
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new TargetInDirectionJob { ECB = _ecb.AsParallelWriter() };
        handle = job.ScheduleParallel(handle);
        return handle;
    }
}