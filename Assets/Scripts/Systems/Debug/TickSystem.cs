// if you ever want to #if this, make sure to set the rate Manager in another system
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ApplyCollisionEventImpactSystem))]
[UpdateBefore(typeof(SpawnSystem))]
[UpdateBefore(typeof(InDirectionStrategySystem))]
[UpdateBefore(typeof(RandomDirectionStrategySystem))]
[UpdateBefore(typeof(CollisionSystem))]
[UpdateBefore(typeof(ColorSystem))]
[UpdateBefore(typeof(DeathSystem))]
[UpdateBefore(typeof(InitDotSystem))]
[UpdateBefore(typeof(InitGhostSystem))]
[UpdateBefore(typeof(LevelSystem))]
[UpdateBefore(typeof(ShootSystem))]
[UpdateBefore(typeof(TriggerSystem))]
[UpdateBefore(typeof(VelocitySystem))]
public partial struct TickSystem : ISystem
{
    private Entity _tickEntity;

    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new SimulationTick { Value = 1 });
        var fixedGroup = state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
        //fixedGroup.RateManager = new FixedRateSimpleManager(Time.fixedDeltaTime);
        fixedGroup.RateManager = new RateUtils.FixedRateCatchUpManager(Time.fixedDeltaTime);
        _tickEntity = SystemAPI.GetSingletonEntity<SimulationTick>();

        //Debug.Log($"[FixedRateSetupSystem] Set FixedRateManager to {Time.fixedDeltaTime}");
    }

    public void OnUpdate(ref SystemState state)
    {
        var tick = SystemAPI.GetSingleton<SimulationTick>().Value;
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

#if DEBUG_SYSTEM_ORDER
        Debug.Log($"_________________________________________________________________________________________________Tick:_{tick}");
#endif
        //Debug.Log($"[{tick.ValueRO.Value}] DeltaTime: {SystemAPI.Time.DeltaTime}, Time: {SystemAPI.Time.ElapsedTime}");

        ecb.SetComponent(_tickEntity, new SimulationTick
        {
            Value = tick + 1
        });
    }
}