using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct InitDotSystem : ISubSystem
{
    private BlobAssetReference<LevelsBlob> _levels;
    private DynamicBuffer<RemoveDotBufferElement> _removedDots;
    private DynamicBuffer<DeadDotBufferElement> _deadDots;
    private RefRW<AliveDotCounterComponent> _counter;
    private Entity _prefab;
    private BlobAssetReference<GameSettingsBlob> _settings;
    private EntityCommandBuffer _ecb;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LevelsBlobComponent>();
        state.RequireForUpdate<DeadDotBufferElement>();
        state.RequireForUpdate<RemoveDotBufferElement>();
        state.RequireForUpdate<AliveDotCounterComponent>();
        state.RequireForUpdate<DotPrefabComponent>();
        state.RequireForUpdate<GameSettingsBlobComponent>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        EntityQuery query = state.GetEntityQuery(new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SpawnRequestEventFlag),
                typeof(KillRequestEventFlag)
            }
        });

        state.RequireAnyForUpdate(query);
    }

    public void OnUpdate(ref SystemState state)
    {
        _levels = SystemAPI.GetSingleton<LevelsBlobComponent>().Blob;
        _removedDots = SystemAPI.GetSingletonBuffer<RemoveDotBufferElement>(false);
        _deadDots = SystemAPI.GetSingletonBuffer<DeadDotBufferElement>(false);
        _counter = SystemAPI.GetSingletonRW<AliveDotCounterComponent>();
        _prefab = SystemAPI.GetSingleton<DotPrefabComponent>().Prefab;
        _settings = SystemAPI.GetSingleton<GameSettingsBlobComponent>().Blob;

        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        if (_counter.ValueRO.AliveDotCounter < _settings.Value.MaxDots)
        {
            foreach (var (init, disable) in SystemAPI.Query<
                RefRO<DotInitComponent>,
                RefRO<DisabledComponent>>())
            {
                AddDotLogic(
                    in disable.ValueRO,
                    in init.ValueRO,
                    ref _levels,
                    ref _removedDots,
                    ref _deadDots,
                    ref _counter,
                    _ecb,
                    _prefab,
                    _settings.Value.MaxDots
                );
            }
        }

        RemovedDotLogic(ref _removedDots, ref _counter, _ecb);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddDotLogic(
        in DisabledComponent disable,
        in DotInitComponent init,
        ref BlobAssetReference<LevelsBlob> levels,
        ref DynamicBuffer<RemoveDotBufferElement> removedDots,
        ref DynamicBuffer<DeadDotBufferElement> deadDots,
        ref RefRW<AliveDotCounterComponent> counter,
        EntityCommandBuffer ecb,
        Entity prefab,
        int maxDots)
    {
        if (counter.ValueRO.AliveDotCounter >= maxDots) return;
        if (disable.Disabled) return;

        var dotEntity = init.DotEntity;
        var position = init.Position;
        var color = init.Color;
        var direction = init.Direction;
        var strategy = init.Strategy;

        int levelIdx = init.LevelNumber - 1;
        ref var lvl = ref levels.Value.Get(levelIdx);

        if (dotEntity == Entity.Null && !GetDot(ecb, ref counter, ref removedDots, ref deadDots, prefab, out dotEntity))
        {
            SetDot(ecb, dotEntity, ref lvl, position, color, direction, strategy, true, ref counter);
        }
        else
        {
            SetDot(ecb, dotEntity, ref lvl, position, color, direction, strategy, false, ref counter);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RemovedDotLogic(
    ref DynamicBuffer<RemoveDotBufferElement> removedDots,
    ref RefRW<AliveDotCounterComponent> counter,
    EntityCommandBuffer ecb)
    {
        var deadAmount = removedDots.Length;
        counter.ValueRW.AliveDotCounter -= deadAmount;
        for (int i = 0; i < deadAmount; i++)
        {
            ecb.AddComponent<Disabled>(removedDots[i].Dot);
        }
        removedDots.Clear();
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var initJob = new InitDotJob
        {
            Levels = _levels,
            RemovedDots = _removedDots,
            DeadDots = _deadDots,
            Counter = _counter,
            Ecb = _ecb,
            Prefab = _prefab,
            MaxDots = _settings.Value.MaxDots
        };

        var removeJob = new RemoveDotsJob
        {
            RemovedDots = _removedDots,
            Counter = _counter,
            Ecb = _ecb
        };

        //don't parrallel as they write buffers
        handle = initJob.Schedule(handle);
        handle = removeJob.Schedule(handle);
        return handle;
    }


    [BurstCompile]
    public partial struct InitDotJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<LevelsBlob> Levels;
        [NativeDisableParallelForRestriction] public DynamicBuffer<RemoveDotBufferElement> RemovedDots;
        [NativeDisableParallelForRestriction] public DynamicBuffer<DeadDotBufferElement> DeadDots;

        [NativeDisableUnsafePtrRestriction] public RefRW<AliveDotCounterComponent> Counter;
        public int MaxDots;

        public Entity Prefab;

        public EntityCommandBuffer Ecb;

        public void Execute(
            in DisabledComponent disable,
            in DotInitComponent init,
            Entity entity,
            [EntityIndexInQuery] int index)
        {
            AddDotLogic(
             in disable,
             in init,
             ref Levels,
             ref RemovedDots,
             ref DeadDots,
             ref Counter,
             Ecb,
             Prefab,
             MaxDots
         );
        }
    }

    [BurstCompile]
    public struct RemoveDotsJob : IJob
    {
        [NativeDisableParallelForRestriction] public DynamicBuffer<RemoveDotBufferElement> RemovedDots;
        
        [NativeDisableUnsafePtrRestriction] public RefRW<AliveDotCounterComponent> Counter;
        public EntityCommandBuffer Ecb;

        public void Execute()
        {
            RemovedDotLogic(ref RemovedDots, ref Counter, Ecb);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool GetDot(
    EntityCommandBuffer ecb,
    ref RefRW<AliveDotCounterComponent> counter,
    ref DynamicBuffer<RemoveDotBufferElement> removedDots,
    ref DynamicBuffer<DeadDotBufferElement> deadDots,
    Entity prefab,
    out Entity result)
    {
        if (removedDots.Length > 0)
        {
            var i = removedDots.Length - 1;
            result = removedDots[i].Dot;
            removedDots.RemoveAt(i);
            return true;
        }

        if (deadDots.Length > 0)
        {
            var i = deadDots.Length - 1;
            result = deadDots[i].Dot;
            deadDots.RemoveAt(i);
            counter.ValueRW.AliveDotCounter++;
            ecb.RemoveComponent<DisabledComponent>(result);
            return true;
        }

        result = ecb.Instantiate(prefab);
        return false;
    }

    //ToDo integrate the maxDot amount as a spawn limit
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetDot(
    EntityCommandBuffer ecb,
    Entity dotEntity,
    ref Level level,
    float3 position,
    float4 color,
    float3 direction,
    Strategy strategy,
    bool isNewDot,
    ref RefRW<AliveDotCounterComponent> counter)
    {
        if (isNewDot)
            counter.ValueRW.AliveDotCounter++;

        var size = level.Size;
        var scaledColor = color * size;

        void SetOrAdd<T>(T component) where T : unmanaged, IComponentData
        {
            if (isNewDot)
                ecb.AddComponent(dotEntity, component);
            else
                ecb.SetComponent(dotEntity, component);
        }

        if (isNewDot)
            ecb.AddComponent(dotEntity, new DotTag());

        SetOrAdd(new DirectionComponent { Direction = direction });
        SetOrAdd(new URPMaterialPropertyBaseColor { Value = color });
        SetOrAdd(new TargetStrategyComponent { Strategy = strategy });
        if (strategy == Strategy.ManualInput && isNewDot)
            ecb.AddComponent(dotEntity, new PlayerTag());
        SetOrAdd(new TargetComponent { Target = Entity.Null });
        SetOrAdd(new ShootTimeComponent { NextAllowedShootTime = 0f });
        SetOrAdd(new TeamComponent { Color = color });
        SetOrAdd(new RGBComponent { Red = scaledColor.x, Green = scaledColor.y, Blue = scaledColor.z });
        SetOrAdd(new LevelComponent { Index = level.Index });
        SetOrAdd(new XPComponent { XP = size });
        SetOrAdd(new LocalTransform
        {
            Position = position,
            Rotation = quaternion.LookRotationSafe(direction, math.up()),
            Scale = DotHelper.GetScale(size)
        });
        SetOrAdd(new CollidedComponent { Collided = false });
    }
}