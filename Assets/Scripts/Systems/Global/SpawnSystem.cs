using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct SpawnSystem : ISubSystem
{
    private DynamicBuffer<InitSpawnedGhostBufferElement> _spawnBuffer;
    private int _counter;
    private double _spawnTime;
    private double _elapsedTime;
    private BlobAssetReference<GameSettingsBlob> _settings;
    private Random _random;
    private EntityCommandBuffer _ecb;
    private Entity _gameEntity;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SpawnTimeComponent>();
        state.RequireForUpdate<InitSpawnedGhostBufferElement>();
        state.RequireForUpdate<RandomComponent>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _spawnTime = SystemAPI.GetSingleton<SpawnTimeComponent>().NextAllowedTime;
        _elapsedTime = SystemAPI.Time.ElapsedTime;
        _settings = SystemAPI.GetSingleton<GameSettingsBlobComponent>().Blob;
        _random = new Random(SystemAPI.GetSingletonRW<RandomComponent>().ValueRW.Value.NextUInt());
        _gameEntity = SystemAPI.GetSingletonEntity<GameTag>();

        _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
        _gameEntity = SystemAPI.GetSingletonEntity<SpawnTimeComponent>();

        _spawnBuffer = SystemAPI.GetSingletonBuffer<InitSpawnedGhostBufferElement>();

        _counter = SystemAPI.GetSingleton<AliveDotCounterComponent>().AliveDotCounter;

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        ExecuteSpawnLogic(_ecb, _gameEntity, _elapsedTime, _spawnTime, ref _spawnBuffer, _settings, ref _random, _counter);
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new SpawnJob
        {
            Time = _elapsedTime,
            SpawnTime = _spawnTime,
            Buffer = _spawnBuffer,
            Settings = _settings,
            Random = _random,
            ECB = _ecb,
            GameEntity = _gameEntity,
            Counter = _counter
        };

        return job.Schedule(handle);
    }

    [BurstCompile]
    public struct SpawnJob : IJob
    {
        public double Time;
        public double SpawnTime;
        [NativeDisableParallelForRestriction] public DynamicBuffer<InitSpawnedGhostBufferElement> Buffer;
        public BlobAssetReference<GameSettingsBlob> Settings;
        public Random Random;
        public EntityCommandBuffer ECB;
        public Entity GameEntity;
        public int Counter;

        public void Execute()
        {
            ExecuteSpawnLogic(ECB, GameEntity, Time, SpawnTime, ref Buffer, Settings, ref Random, Counter);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteSpawnLogic(EntityCommandBuffer ecb, Entity gameEntity, double time, double spawnTime, ref DynamicBuffer<InitSpawnedGhostBufferElement> buffer, BlobAssetReference<GameSettingsBlob> settingsBlob, ref Random random, int counter)
    {
        if (time < spawnTime) return;
        ref var settings = ref settingsBlob.Value;
        if (counter >= settings.MaxDots) return;

        ecb.SetComponent(gameEntity, new SpawnTimeComponent { NextAllowedTime = time + 1.0 });
        ecb.AddComponent<GhostRequestEventFlag>(gameEntity);

        for (int i = 0; i < settings.DotsPerWave && (counter+i) < settings.MaxDots; i++)
        {
            var x = random.NextFloat(-400, 400);
            var z = random.NextFloat(-150, 150);
            var randomPosition = new float3(x, 0, z);
            var levelNumber = random.NextInt(1, settings.DotMaxSpawnLevel);
            var levelIndex = levelNumber - 1;
            var radius = DotHelper.GetHalfScale(levelIndex);

            float angle = random.NextFloat(0f, math.PI * 2f);
            float3 forward = math.forward();
            float3 randomDirection = math.mul(quaternion.RotateY(angle), forward);

            int randomTeam = random.NextInt(0, 3);
            Team team = randomTeam switch
            {
                0 => Team.Red,
                1 => Team.Blue,
                2 => Team.Green
            };

            buffer.Add(new InitSpawnedGhostBufferElement
            {
                LevelIndex = levelIndex,
                Strategy = Strategy.Random,
                Position = randomPosition,
                Color = DotHelper.GetColor(team),
                Direction = randomDirection
            });
        }
    }
}
