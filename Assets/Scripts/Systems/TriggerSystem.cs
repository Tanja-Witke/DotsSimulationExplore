using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
public partial struct TriggerSystem : ISubSystem
{
    private ComponentLookup<DisabledComponent> _disableLookup;
    private SimulationSingleton _sim;
    private NativeReference<int> _spawnCounter;
    private EntityCommandBuffer.ParallelWriter _ecb;
    private Entity _gameEntity;

    [BurstCompile]
    public struct DisableGhostTriggerEventJob : ITriggerEventsJob
    {
        public ComponentLookup<DisabledComponent> DisableLookup;
        public NativeReference<int> SpawnCounter;

        public void Execute(TriggerEvent triggerEvent)
        {
            var entityA = triggerEvent.EntityA;
            var entityB = triggerEvent.EntityB;

            // Disable Ghosts (Dots trying to spawn) when they trigger with something -> no space available to spawn
            if (DisableLookup.HasComponent(entityA))
            {
                DisableLookup[entityA] = new DisabledComponent { Disabled = true };
                SpawnCounter.Value--;
            }
            if (DisableLookup.HasComponent(entityB))
            {
                DisableLookup[entityB] = new DisabledComponent { Disabled = true };
                SpawnCounter.Value--;
            }
        }
    }

    // write an IJob that takes the SpawnCounter and an EntityCommandBuffer and a gameEntity and then adds a SpawnRequestEventFlag
    [BurstCompile]
    public struct EmitSpawnRequestEventJob : IJob
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        public Entity GameEntity;
        public NativeReference<int> SpawnCounter;
        public void Execute()
        {
            if (SpawnCounter.Value > 0)
            {
                ECB.AddComponent<SpawnRequestEventFlag>(0, GameEntity);
                SpawnCounter.Value = 0;
            }
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
        state.RequireForUpdate<DisabledComponent>();
        state.RequireForUpdate<GhostTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _sim = SystemAPI.GetSingleton<SimulationSingleton>();
        _disableLookup = state.GetComponentLookup<DisabledComponent>(isReadOnly: false);

        var counter = SystemAPI.QueryBuilder()
            .WithAll<GhostTag>()
            .Build().CalculateEntityCount();
        _spawnCounter = new NativeReference<int>(counter, Allocator.TempJob);
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        _gameEntity = SystemAPI.GetSingletonEntity<GameTag>();

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        Schedule(state.Dependency, ref state).Complete();
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        handle = new DisableGhostTriggerEventJob
        {
            DisableLookup = _disableLookup,
            SpawnCounter = _spawnCounter
        }.Schedule(_sim, handle);

        handle = new EmitSpawnRequestEventJob
        {
            ECB = _ecb,
            GameEntity = _gameEntity,
            SpawnCounter = _spawnCounter
        }.Schedule(handle);
        return handle;
    }
}