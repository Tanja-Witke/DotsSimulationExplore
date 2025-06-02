using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct DeathSystem : ISubSystem
{
    private NativeList<RemoveDotBufferElement> _removalList;
    private DynamicBuffer<RemoveDotBufferElement> _buffer;
    private bool _initialized;
    private Entity _gameEntity;
    private EntityCommandBuffer.ParallelWriter _ecb;

    [BurstCompile]
    public partial struct DeathJob : IJobEntity
    {
        public NativeList<RemoveDotBufferElement>.ParallelWriter ParallelBuffer;

        public void Execute(Entity dotEntity, [WithChangeFilter] in XPComponent xp)
        {
            DeathLogic(xp.XP, dotEntity, ref ParallelBuffer);
        }
    }

    [BurstCompile]
    public struct FlushBufferJob : IJob
    {
        public NativeList<RemoveDotBufferElement> Source;
        [NativeDisableParallelForRestriction] public DynamicBuffer<RemoveDotBufferElement> Buffer;

        public EntityCommandBuffer.ParallelWriter ECB;
        public Entity GameEntity;

        public void Execute()
        {
            if (Source.Length == 0) return;
            Buffer.AddRange(Source.AsArray());
            ECB.AddComponent<KillRequestEventFlag>(0, GameEntity);
            Source.Clear();
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<XPComponent>();
        state.RequireForUpdate<RemoveDotBufferElement>();

        state.RequireForUpdate<ImpactEventFlag>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_removalList.IsCreated)
            _removalList.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        _buffer = SystemAPI.GetSingletonBuffer<RemoveDotBufferElement>();

        if (!_initialized)
        {
            var settings = SystemAPI.GetSingleton<GameSettingsBlobComponent>().Blob;
            _removalList = new NativeList<RemoveDotBufferElement>(settings.Value.MaxDots, Allocator.Persistent); 
            _initialized = true;
        }

        _gameEntity = SystemAPI.GetSingletonEntity<GameTag>();
        _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (xp, dotEntity) in SystemAPI.Query<
            RefRO<XPComponent>>()
            .WithEntityAccess())
        {
            DeathLogic(xp.ValueRO.XP, dotEntity, ref _removalList);
        }

        if (_removalList.Length == 0) return;
        _buffer.AddRange(_removalList.AsArray());
        _removalList.Clear();
        _ecb.AddComponent<KillRequestEventFlag>(0, _gameEntity);
    }

    //ToDo find a way to not have redundant code here. There is an Interface withou Add()
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeathLogic(int xp, Entity dotEntity, ref NativeList<RemoveDotBufferElement>.ParallelWriter buffer)
    {
        if (xp <= 0)
        {
            buffer.AddNoResize(new RemoveDotBufferElement { Dot = dotEntity });
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DeathLogic(int xp, Entity dotEntity, ref NativeList<RemoveDotBufferElement> buffer)
    {
        if (xp <= 0)
        {
            buffer.Add(new RemoveDotBufferElement { Dot = dotEntity });
        }
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var deathJob = new DeathJob
        {
            ParallelBuffer = _removalList.AsParallelWriter()
        };
        handle = deathJob.ScheduleParallel(handle);

        var flushJob = new FlushBufferJob
        {
            Source = _removalList,
            Buffer = _buffer,
            ECB = _ecb,
            GameEntity = _gameEntity
        };
        handle = flushJob.Schedule(handle);

        return handle;
    }
}