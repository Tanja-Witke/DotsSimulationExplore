using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct VelocitySystem : ISubSystem
{
    private BlobAssetReference<LevelsBlob> _levels;
    private bool _initialized;
    private EntityCommandBuffer _ecb;
    private float _deltaTime;

    [BurstCompile]
    public partial struct VelocityJob : IJobEntity
    {
        public BlobAssetReference<LevelsBlob> Levels;
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(
            [EntityIndexInQuery] int index,
            in PhysicsVelocity velocity,
            in DirectionComponent direction,
            in LevelComponent levelComponent,
            in PhysicsMass mass,
            Entity dotEntity)
        {
            ref var levels = ref Levels.Value;
            Logic(index, dotEntity, ECB, in velocity, direction.Direction, levelComponent.Index, mass.InverseMass, DeltaTime, ref levels);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DotTag>();
        state.RequireForUpdate<LevelsBlobComponent>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        if (!_initialized)
        {
            _levels = SystemAPI.GetSingleton<LevelsBlobComponent>().Blob;
            _initialized = true;
        }

        _deltaTime = SystemAPI.Time.DeltaTime;

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        ref var levels = ref _levels.Value;
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (velocity, direction, levelComponent, mass, dotEntity) in SystemAPI.Query<
            RefRO<PhysicsVelocity>,
            RefRO<DirectionComponent>,
            RefRO<LevelComponent>,
            RefRO<PhysicsMass>>()
            .WithEntityAccess())
        {
            Logic(0, dotEntity, _ecb.AsParallelWriter(), in velocity.ValueRO, direction.ValueRO.Direction, levelComponent.ValueRO.Index, mass.ValueRO.InverseMass, deltaTime, ref levels);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Logic(
        int index,
        Entity dotEntity,
        EntityCommandBuffer.ParallelWriter ecb,
        in PhysicsVelocity velocity,
        float3 direction,
        int levelIndex,
        float inverseMass,
        float deltaTime,
        ref LevelsBlob levels)
    {
        float3 desiredDirection = math.normalizesafe(direction);
        ref var level = ref levels.Get(levelIndex);
        float desiredSpeed = level.Speed;
        float forceStrength = 20f;

        float3 desiredVelocity = desiredDirection * desiredSpeed;
        float3 currentVelocity = velocity.Linear;

        var velocityComponent = velocity;
        if (math.all(currentVelocity == float3.zero))
        {
            velocityComponent.Linear = desiredVelocity;
            ecb.SetComponent(index, dotEntity, velocityComponent);            
            return;
        }

        float3 impulseDirection = desiredVelocity - currentVelocity;

        if (math.lengthsq(impulseDirection) > 0.0001f)
            impulseDirection = math.normalizesafe(impulseDirection);

        float3 impulse = impulseDirection * forceStrength * deltaTime;
        float3 velocityImpact = impulse * inverseMass;

        velocityComponent.Linear += velocityImpact;

        float speed = math.length(velocity.Linear);
        if (speed > desiredSpeed)
        {
            velocityComponent.Linear = math.normalize(velocity.Linear) * desiredSpeed;
        }
        ecb.SetComponent(index, dotEntity, velocityComponent);
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new VelocityJob
        {
            Levels = _levels,
            ECB = _ecb.AsParallelWriter(),
            DeltaTime = _deltaTime
        };
        handle = job.ScheduleParallel(handle);
        return handle;
    }
}



