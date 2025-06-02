using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct RandomDirectionStrategySystem : ISubSystem
{
    private Random _random;
    private EntityCommandBuffer _ecb;

    [BurstCompile]
    public partial struct RandomDirectionJob : IJobEntity
    {
        public Random Random;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(
            [EntityIndexInQuery] int index,
            Entity dotEntity,
            [WithChangeFilter] in CollidedComponent changeTarget,
            in DirectionComponent direction,
            in TargetStrategyComponent targetStrategy)
        {
            RandomDirectionLogic(index, dotEntity, ECB, in changeTarget, in direction, targetStrategy.Strategy, ref Random);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DotTag>();
        state.RequireForUpdate<RandomComponent>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        state.RequireForUpdate<CollisionEventFlag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var randomComponent = SystemAPI.GetSingletonRW<RandomComponent>();
        var seed = randomComponent.ValueRW.Value.NextUInt();
        _random = new Random(seed);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        // Todo save all queries into fields to share with the jobs
        foreach (var (changeTarget, direction, targetStrategy, dotEntity) in SystemAPI.Query<
            RefRO<CollidedComponent>,
            RefRO<DirectionComponent>,
            RefRO<TargetStrategyComponent>>()
            .WithChangeFilter<CollidedComponent>()
            .WithEntityAccess())
        {
            RandomDirectionLogic(0, dotEntity, _ecb.AsParallelWriter(), in changeTarget.ValueRO, in direction.ValueRO, targetStrategy.ValueRO.Strategy, ref _random);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RandomDirectionLogic(
        int index,
        Entity dotEntity,
        EntityCommandBuffer.ParallelWriter ecb, 
        in CollidedComponent changeTarget, 
        in DirectionComponent direction, 
        Strategy strategy, 
        ref Random random)
    {
        if (strategy != Strategy.Random || changeTarget.Collided == false)
            return;

        float angle = random.NextFloat(0f, 2f * math.PI);
        float3 randomDirection = new float3(math.cos(angle), 0f, math.sin(angle));

        ecb.SetComponent(index, dotEntity, new DirectionComponent { Direction = randomDirection });
        ecb.SetComponent(index, dotEntity, new CollidedComponent { Collided = false });
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new RandomDirectionJob
        {
            Random = _random,
            ECB = _ecb.AsParallelWriter()
        };

        handle = job.ScheduleParallel(handle);
        return handle;
    }
}