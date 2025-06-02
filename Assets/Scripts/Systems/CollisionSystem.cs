using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;


[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSimulationGroup))]
public partial struct CollisionSystem : ISubSystem
{
    private NativeParallelHashSet<CollisionPair> _collisionPairs;

    private int _aliveDotCount;
    private SimulationSingleton _sim;
    private DynamicBuffer<CollisionEventBufferElement> _buffer;
    private EntityCommandBuffer _ecb;
    private Entity _gameEntity;

    [BurstCompile]
    public struct CollisionEventJob : ICollisionEventsJob
    {
        public NativeParallelHashSet<CollisionPair>.ParallelWriter SeenPairs;

        public void Execute(CollisionEvent collisionEvent)
        {
            SeenPairs.Add(new CollisionPair(collisionEvent.EntityA, collisionEvent.EntityB, collisionEvent.Normal));
        }
    }

    [BurstCompile]
    public struct EmitCollisionEventsJob : IJob
    {
        [ReadOnly] public NativeParallelHashSet<CollisionPair> CollisionPairs;

        [NativeDisableParallelForRestriction]
        public DynamicBuffer<CollisionEventBufferElement> Buffer;

        public EntityCommandBuffer ECB;
        public Entity GameEntity;

        public void Execute()
        {
            EmmitCollisionEventLogic(ECB.AsParallelWriter(), GameEntity, ref Buffer, ref CollisionPairs);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
        state.RequireForUpdate<AliveDotCounterComponent>();
        state.RequireForUpdate<CollisionEventBufferElement>();
        state.RequireForUpdate<DotTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _sim = SystemAPI.GetSingleton<SimulationSingleton>();
        _aliveDotCount = SystemAPI.GetSingleton<AliveDotCounterComponent>().AliveDotCounter;
        _buffer = SystemAPI.GetSingletonBuffer<CollisionEventBufferElement>();

        _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        _gameEntity = SystemAPI.GetSingletonEntity<SpawnTimeComponent>();

        int capacity = _aliveDotCount * _aliveDotCount + 2 * _aliveDotCount;
        EnsureCapacity(capacity);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        var handle = GetCollisionEventJob(state.Dependency);
        handle.Complete();

        EmmitCollisionEventLogic(_ecb.AsParallelWriter(), _gameEntity, ref _buffer, ref _collisionPairs);
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EmmitCollisionEventLogic(EntityCommandBuffer.ParallelWriter ecb, Entity gameEntity,  ref DynamicBuffer<CollisionEventBufferElement> buffer, ref NativeParallelHashSet<CollisionPair> collisionPairs)
    {
        buffer.Clear();

        int count = collisionPairs.Count();

        if (count > 0)
        {
            ecb.AddComponent<CollisionEventFlag>(0, gameEntity);
        }

        foreach (var pair in collisionPairs)
        {
            // A→B
            buffer.Add(new CollisionEventBufferElement
            {
                Source = pair.A,
                Target = pair.B
            });

            // B→A
            buffer.Add(new CollisionEventBufferElement
            {
                Source = pair.B,
                Target = pair.A
            });
        }
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        handle = GetCollisionEventJob(handle);

        var emitJob = new EmitCollisionEventsJob
        {
            CollisionPairs = _collisionPairs,
            Buffer = _buffer,
            GameEntity = _gameEntity,
            ECB = _ecb
        };

        handle = emitJob.Schedule(handle);

        return handle;
    }

    public JobHandle GetCollisionEventJob(JobHandle handle)
    {
        var collisionJob = new CollisionEventJob
        {
            SeenPairs = _collisionPairs.AsParallelWriter()
        };

        return collisionJob.Schedule(_sim, handle);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_collisionPairs.IsCreated)
        {
            _collisionPairs.Dispose();
        }
    }

    private void EnsureCapacity(int capacity)
    {
        //ToDo add generic "ensure capacity" method
        if (_collisionPairs.IsCreated)
        {
            if (_collisionPairs.Capacity < capacity)
            {
                _collisionPairs.Dispose();
                _collisionPairs = new NativeParallelHashSet<CollisionPair>(capacity, Allocator.Persistent);
            }
            else
            {
                _collisionPairs.Clear();
            }
        }
        else
        {
            _collisionPairs = new NativeParallelHashSet<CollisionPair>(capacity, Allocator.Persistent);
        }
    }
}

public struct CollisionPair : IEquatable<CollisionPair>
{
    public Entity A;
    public Entity B;
    public float3 Normal;

    public CollisionPair(Entity a, Entity b, float3 normal)
    {
        A = a;
        B = b;
        Normal = normal;
    }

    public bool Equals(CollisionPair other) => A == other.A && B == other.B;

    public override bool Equals(object obj) => obj is CollisionPair other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + A.GetHashCode();
            hash = hash * 31 + B.GetHashCode();
            return hash;
        }
    }
}
