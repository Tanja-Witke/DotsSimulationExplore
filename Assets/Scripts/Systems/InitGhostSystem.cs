using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct InitGhostSystem : ISubSystem
{
    private DynamicBuffer<InitShootedGhostBufferElement> _shootedGhosts;
    private DynamicBuffer<InitSpawnedGhostBufferElement> _spawnedGhosts;
    private Entity _prefab;
    private static int _i = 0; // temporary Static index to keep track of the current index in the buffers
    private Entity _singletonShootEntity;
    private Entity _singletonSpawnEntity;

    private EntityCommandBuffer _ecb;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<InitShootedGhostBufferElement>();
        state.RequireForUpdate<InitSpawnedGhostBufferElement>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        state.RequireForUpdate<GhostRequestEventFlag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _singletonShootEntity = SystemAPI.GetSingletonEntity<InitShootedGhostBufferElement>();
        _singletonSpawnEntity = SystemAPI.GetSingletonEntity<InitSpawnedGhostBufferElement>();

        _shootedGhosts = state.GetBufferLookup<InitShootedGhostBufferElement>(true)[_singletonShootEntity];
        _spawnedGhosts = state.GetBufferLookup<InitSpawnedGhostBufferElement>(true)[_singletonSpawnEntity];
        _prefab = SystemAPI.GetSingleton<GhostPrefabComponent>().Prefab;
      
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (tag, ghostEntity) in SystemAPI.Query<
                     RefRO<GhostTag>>()
                 .WithEntityAccess())
        {
            GhostAssignmentLogic(
                in _shootedGhosts,
                in _spawnedGhosts,
                ghostEntity,
                _ecb
            );
        }

        AssignRemainingGhostsLogic(in _shootedGhosts, _singletonShootEntity, in _spawnedGhosts, _singletonSpawnEntity, _prefab, _ecb);
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var initJob = new InitGhostJob
        {
            ShootedGhosts = _shootedGhosts,
            SpawnedGhosts = _spawnedGhosts,
            Ecb = _ecb
        };

        var spawnJob = new SpawnGhostsJob
        {
            ShootedGhosts = _shootedGhosts,
            ShootedSingletonBufferEntity = _singletonShootEntity,
            SpawnedGhosts = _spawnedGhosts,
            SpawnedSingletonBufferEntity = _singletonSpawnEntity,
            Ecb = _ecb,
            Prefab = _prefab
        };

        // don't schedule in parallel as they read linear from buffers
        handle = initJob.Schedule(handle);
        handle = spawnJob.Schedule(handle);
        return handle;
    }

    [BurstCompile]
    public partial struct InitGhostJob : IJobEntity
    {
        [ReadOnly] public DynamicBuffer<InitShootedGhostBufferElement> ShootedGhosts;
        [ReadOnly] public DynamicBuffer<InitSpawnedGhostBufferElement> SpawnedGhosts;
        public EntityCommandBuffer Ecb;

        public void Execute(
            Entity ghostEntity,
            in GhostTag tag)
        {
            GhostAssignmentLogic(
                in ShootedGhosts,
                in SpawnedGhosts,
                ghostEntity,
                Ecb
            );
        }
    }

    [BurstCompile]
    public struct SpawnGhostsJob : IJob
    {
        [ReadOnly] public DynamicBuffer<InitShootedGhostBufferElement> ShootedGhosts;
        [ReadOnly] public DynamicBuffer<InitSpawnedGhostBufferElement> SpawnedGhosts;
        public Entity ShootedSingletonBufferEntity;
        public Entity SpawnedSingletonBufferEntity;
        public EntityCommandBuffer Ecb;
        public Entity Prefab;

        public void Execute()
        {
            AssignRemainingGhostsLogic(in ShootedGhosts, ShootedSingletonBufferEntity, in SpawnedGhosts, SpawnedSingletonBufferEntity, Prefab, Ecb);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GhostAssignmentLogic(
        in DynamicBuffer<InitShootedGhostBufferElement> shooted,
        in DynamicBuffer<InitSpawnedGhostBufferElement> spawned,
        Entity ghostEntity,
        EntityCommandBuffer ecb)
    {
        // Todo refactor: the static int index is a temp solution to see if I can have the buffers as read only
        if (TryGetInitDataFromBuffer(_i++, in shooted, in spawned, out var initComponent))
        {
            ecb.SetComponent(ghostEntity, initComponent);
            ecb.SetComponent(ghostEntity, new DisabledComponent { Disabled = false });
        }
        else
        {
            //Todo concider disabling the entity and reviving if the triggering gets too expensive
            ecb.SetComponent(ghostEntity, new DisabledComponent { Disabled = true });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssignRemainingGhostsLogic(
        in DynamicBuffer<InitShootedGhostBufferElement> shooted,
        Entity shootedSingletonBufferEntity,
        in DynamicBuffer<InitSpawnedGhostBufferElement> spawned,
        Entity spawnedSingletonBufferEntity,
        Entity prefab,
        EntityCommandBuffer ecb)
    {
        //ToDo reactivate dead ghosts before creating new ones
        while (TryGetInitDataFromBuffer(_i++, in shooted, in spawned, out var initComponent))
        {
            var ghostEntity = ecb.Instantiate(prefab);
            ecb.AddComponent<GhostTag>(ghostEntity);
            ecb.AddComponent(ghostEntity, new DisabledComponent { Disabled = false});
            ecb.AddComponent(ghostEntity, initComponent);
        }
        ecb.SetBuffer<InitShootedGhostBufferElement>(shootedSingletonBufferEntity).Clear();
        ecb.SetBuffer<InitSpawnedGhostBufferElement>(spawnedSingletonBufferEntity).Clear();
        _i = 0; // reset static index for next frame
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetInitDataFromBuffer(
        int index,
        in DynamicBuffer<InitShootedGhostBufferElement> shooted,
        in DynamicBuffer<InitSpawnedGhostBufferElement> spawned,
        out DotInitComponent init)
    {
        init = default;
        if (shooted.Length > index)
        {
            var data = shooted[index];
            SetInitDot(ref init, data.LevelIndex, data.Strategy, data.Position, data.Color, data.Direction);
            return true;
        }

        index -= shooted.Length; // adjust index to check spawned buffer

        if (spawned.Length > index)
        {
            var data = spawned[index];
            SetInitDot(ref init, data.LevelIndex, data.Strategy, data.Position, data.Color, data.Direction);
            return true;
        }

        return false;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetInitDot(
        ref DotInitComponent init,
        int levelIndex,
        Strategy strategy,
        float3 position,
        float4 color,
        float3 direction)
    {
        init.LevelNumber = levelIndex + 1;
        init.Strategy = strategy;
        init.Position = position;
        init.Color = color;
        init.Direction = direction;
        init.DotEntity = Entity.Null;
    }
}

