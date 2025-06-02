using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct LevelSystem : ISubSystem
{
    private BlobAssetReference<LevelsBlob> _levels;
    private bool _initialized;
    private EntityCommandBuffer _ecb;

    [BurstCompile]
    public partial struct LevelJob : IJobEntity
    {
        public BlobAssetReference<LevelsBlob> Levels;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute([EntityIndexInQuery] int index, in LevelComponent level, in LocalTransform transform, [WithChangeFilter] in XPComponent xp, Entity dotEntity)
        {
            Logic(dotEntity, ref Levels.Value, in level, in transform, xp.XP, ECB, index);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LevelsBlobComponent>();
        state.RequireForUpdate<DotTag>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        state.RequireForUpdate<ImpactEventFlag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        if (!_initialized)
        {
            _levels = SystemAPI.GetSingleton<LevelsBlobComponent>().Blob;
            _initialized = true;
        }

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (level, transform, xp, dotEntity) in SystemAPI.Query<
            RefRO<LevelComponent>,
            RefRO<LocalTransform>,
            RefRO<XPComponent>>()
            .WithEntityAccess()
            .WithChangeFilter<XPComponent>())
        {
            Logic(dotEntity, ref _levels.Value, in level.ValueRO, in transform.ValueRO, xp.ValueRO.XP, _ecb.AsParallelWriter(), 0);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Logic(Entity dotEntity, ref LevelsBlob levels, in LevelComponent level, in LocalTransform transform, int xp, EntityCommandBuffer.ParallelWriter ecb, int index)
    {
        ref var newLevel = ref levels.GetFromXP(xp);
        if (level.Index == newLevel.Index) return;

        ecb.SetComponent(index, dotEntity, new LevelComponent { Index = newLevel.Index });

        var newTransform = transform;
        newTransform.Scale = DotHelper.GetScale(newLevel.Size);
        ecb.SetComponent(index, dotEntity, newTransform);
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new LevelJob { Levels = _levels, ECB = _ecb.AsParallelWriter() };
        handle = job.ScheduleParallel(handle);
        return handle;
    }
}

