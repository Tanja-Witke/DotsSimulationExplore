using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ApplyCollisionEventImpactSystem : ISubSystem
{
    private NativeParallelMultiHashMap<Entity, ImpactData> _impacts;

    private ComponentLookup<LevelComponent> _levelLookup;
    private ComponentLookup<TeamComponent> _teamLookup;

    //private int _aliveDotCount;
    private BlobAssetReference<LevelRelationshipsBlob> _levelRelationships;
    private DynamicBuffer<CollisionEventBufferElement> _buffer;
    private Entity _gameEntity;
    private EntityCommandBuffer _ecb;

    private int _lastTick;

    public struct ImpactData
    {
        public int XP;
        public float3 ColorImpact;
    }


    [BurstCompile]
    public struct FillImpactJob : IJob
    {
        [NativeDisableParallelForRestriction]
        [ReadOnly] public DynamicBuffer<CollisionEventBufferElement> Buffer;

        [ReadOnly] public ComponentLookup<LevelComponent> LevelLookup;
        [ReadOnly] public ComponentLookup<TeamComponent> TeamLookup;
        [ReadOnly] public BlobAssetReference<LevelRelationshipsBlob> Relationships;

        public NativeParallelMultiHashMap<Entity, ImpactData> Impacts;
        public EntityCommandBuffer ECB;
        public Entity EventEntity;

        public void Execute()
        {
            FillLogic(ECB.AsParallelWriter(), EventEntity, in Buffer, in LevelLookup, in TeamLookup, Relationships, ref Impacts);
        }
    }

    [BurstCompile]
    public partial struct ApplyImpactJob : IJobEntity
    {
        [ReadOnly] public NativeParallelMultiHashMap<Entity, ImpactData> Impacts;

        public void Execute(
            ref XPComponent xp,
            ref RGBComponent rgb,
            ref CollidedComponent collided,
            [EntityIndexInQuery] int index,
            Entity targetDotEntity)
        {       
            ApplyImpactLogic(ref xp, ref rgb, ref collided, ref Impacts, targetDotEntity);
        }
    }


    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AliveDotCounterComponent>();
        state.RequireForUpdate<LevelRelationshipsBlobComponent>();
        state.RequireForUpdate<DotTag>();
        state.RequireForUpdate<CollisionEventBufferElement>();

        state.RequireForUpdate<CollisionEventFlag>();
        _lastTick = -1;

    }

    public void OnUpdate(ref SystemState state)
    {
        var tick = SystemAPI.GetSingleton<SimulationTick>().Value;

        //ToDo find a better way to handle this
        if (_lastTick == tick) return;
        _lastTick = tick;
        _levelLookup = state.GetComponentLookup<LevelComponent>(isReadOnly: true);
        _teamLookup = state.GetComponentLookup<TeamComponent>(isReadOnly: true);
        _levelRelationships = SystemAPI.GetSingleton<LevelRelationshipsBlobComponent>().Blob;
        _buffer = SystemAPI.GetSingletonBuffer<CollisionEventBufferElement>();
        _gameEntity = SystemAPI.GetSingletonEntity<GameTag>();

        _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

        var aliveDotCount = SystemAPI.GetSingleton<AliveDotCounterComponent>().AliveDotCounter;

        int estimatedCollisions = aliveDotCount * 4;
        EnsureCapacity(estimatedCollisions);

        BaseSystem.OnUpdate(ref this, ref state); 
    }

    public void Update(ref SystemState state)
    {
        FillLogic(_ecb.AsParallelWriter(), _gameEntity, in _buffer, in _levelLookup, in _teamLookup, _levelRelationships, ref _impacts);

        foreach (var (xp, rgb, collidedData, targetDotEntity) in SystemAPI.Query<
            RefRW<XPComponent>,
            RefRW<RGBComponent>,
            RefRW<CollidedComponent>>()
            .WithEntityAccess())
        {
            ApplyImpactLogic(ref xp.ValueRW, ref rgb.ValueRW, ref collidedData.ValueRW, ref _impacts, targetDotEntity);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FillLogic(
        EntityCommandBuffer.ParallelWriter ecb,
        Entity gameEntity,
        in DynamicBuffer<CollisionEventBufferElement> events, 
        in ComponentLookup<LevelComponent> levelLookup, 
        in ComponentLookup<TeamComponent> teamLookup, 
        BlobAssetReference<LevelRelationshipsBlob> relationships, 
        ref NativeParallelMultiHashMap<Entity, ImpactData> impacts)
    {
        int n = events.Length;

        if ( n > 0)
        {
            //Todo find better inices for parallel ecbs in non entity jobs
            ecb.AddComponent<ImpactEventFlag>(0, gameEntity);
        }

        for (int i = 0; i < n; i++)
        {
            var evt = events[i];
            var source = evt.Source;
            var target = evt.Target;

            bool sourceIsDot = levelLookup.HasComponent(source);
            bool targetIsDot = levelLookup.HasComponent(target);

            ImpactData impact = default;

            if (sourceIsDot && targetIsDot)
            {
                var srcColor = teamLookup[source].Color;
                var tgtColor = teamLookup[target].Color;
                bool sameTeam = srcColor.Equals(tgtColor);

                int srcLevel = levelLookup[source].Index;
                int tgtLevel = levelLookup[target].Index;

                var (xp, colorImpactInt) = relationships.Value.Get(srcLevel, tgtLevel, sameTeam);
                float3 colorImpact = ComputeColorImpact(srcColor, colorImpactInt);

                impact = new ImpactData
                {
                    XP = xp,
                    ColorImpact = colorImpact
                };
            }

            if (targetIsDot)
                impacts.Add(target, impact);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyImpactLogic(
                ref XPComponent xp,
                ref RGBComponent rgb,
                ref CollidedComponent collided,
                ref NativeParallelMultiHashMap<Entity, ImpactData> impacts,
                Entity targetDotEntity)
    {
        if (impacts.TryGetFirstValue(targetDotEntity, out var impact, out var it))
        {
            do
            {
                xp.XP += impact.XP;
                rgb.Red += impact.ColorImpact.x;
                rgb.Green += impact.ColorImpact.y;
                rgb.Blue += impact.ColorImpact.z;
                collided.Collided = true;
            }
            while (impacts.TryGetNextValue(out impact, ref it));
        }
    }

    public JobHandle Schedule(JobHandle dependencies, ref SystemState state)
    {
        var fillJob = new FillImpactJob
        {
            LevelLookup = _levelLookup,
            TeamLookup = _teamLookup,
            Relationships = _levelRelationships,
            Impacts = _impacts,
            Buffer = _buffer,
            ECB = _ecb,
            EventEntity = _gameEntity
        };

        var handle = fillJob.Schedule(dependencies);

        var applyJob = new ApplyImpactJob
        {
            Impacts = _impacts
        };

        handle = applyJob.ScheduleParallel(handle);

        return handle;
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_impacts.IsCreated)
        {
            _impacts.Dispose();
        }
    }

    private void EnsureCapacity(int capacity)
    {
        //ToDo add generic "ensure capacity" method
        if (_impacts.IsCreated)
        {
            if (_impacts.Capacity < capacity)
            {
                _impacts.Dispose();
                _impacts = new NativeParallelMultiHashMap<Entity, ImpactData>(capacity, Allocator.Persistent);
            }
            else
            {
                _impacts.Clear();
            }
        }
        else
        {
            _impacts = new NativeParallelMultiHashMap<Entity, ImpactData>(capacity, Allocator.Persistent);
        }
    }

    static float3 ComputeColorImpact(float4 color, int impact)
    {
        float total = color.x + color.y + color.z;
        return total > 0f
            ? new float3(color.x, color.y, color.z) * (impact / total)
            : float3.zero;
    }
}

