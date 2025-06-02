/*using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ApplyCollisionEventImpactSystem))]
public partial struct ChangeTargetSystem : ISubSystem
{
    private ChangeTargetJob _job;
    private bool _jobInitialized;

    [BurstCompile]
    public partial struct ChangeTargetJob : IJobEntity
    {
        public void Execute(ref ChangeTargetComponent changeTarget, [WithChangeFilter] ref CollidedComponent collided)
        {
            Logic(ref changeTarget, ref collided);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NormalScheduling>();
        state.RequireForUpdate<ChangeTargetComponent>();
        state.RequireForUpdate<CollidedComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_jobInitialized)
        {
            _job = new ChangeTargetJob();
            _jobInitialized = true;
        }

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (changeTarget, collided) in SystemAPI.Query<
            RefRW<ChangeTargetComponent>,
            RefRW<CollidedComponent>>()
            .WithChangeFilter<CollidedComponent>())
        {
            Logic(ref changeTarget.ValueRW, ref collided.ValueRW);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Logic(ref ChangeTargetComponent changeTarget, ref CollidedComponent collided)
    {
        if (!collided.Collided) return;

        if (!changeTarget.ShouldChange)
        {
            changeTarget.ShouldChange = true;
        }

        collided.Collided = false;
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        handle = _job.ScheduleParallel(handle);
        return handle;
    }
}



*//*using Unity.Burst;
using Unity.Entities;
using UnityEngine;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ApplyCollisionEventImpactSystem))]
public partial struct ChangeTargetSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
#if DEBUG_SYSTEM_ORDER
        var tick = SystemAPI.GetSingleton<SimulationTick>();
        Debug.Log($"8. ChangeTargetSystem: {tick.Value}");
#endif
#if DEBUG_SYSTEM_DURATION
        TimingTrackerSystem.Start(GetType().Name);
#endif
        var em = state.EntityManager;

        *//*// Target should be changed when the old one died
        foreach (var (target, changeTarget) in SystemAPI.Query<RefRO<TargetComponent>, RefRW<ChangeTargetComponent>>())
        {
            var targetEntity = target.ValueRO.Target;
            if (em.Exists(targetEntity) && em.IsEnabled(targetEntity))
            {
                continue;
            }

            if (changeTarget.ValueRO.ShouldChange == false)
            {
                changeTarget.ValueRW.ShouldChange = true;
            }
        }*//*

        // ToDo investigate change filter
        foreach (var (changeTarget, collided) in SystemAPI.Query<
            RefRW<ChangeTargetComponent>,
            RefRW<CollidedComponent>>()
            .WithChangeFilter<CollidedComponent>()
            )
        {
            if (!collided.ValueRO.Collided) continue;

            if (changeTarget.ValueRO.ShouldChange == false)
            {
                changeTarget.ValueRW.ShouldChange = true;
            }

            // ToDo concider doing this in a cleanup System
            collided.ValueRW.Collided = false;
#if DEBUG_SYSTEM_DURATION
            var tick = SystemAPI.GetSingletonRW<SimulationTick>();
            TimingTrackerSystem.Stop(GetType().Name, tick.ValueRO.Value);
#endif
        }
    }
}
*/