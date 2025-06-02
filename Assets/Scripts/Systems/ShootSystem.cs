using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct ShootSystem : ISubSystem
{
    private BlobAssetReference<LevelsBlob> _levels;
    private double _elapsedTime;
    private DynamicBuffer<InitShootedGhostBufferElement> _shootBuffer;
    private EntityCommandBuffer.ParallelWriter _ecb;
    private Entity _gameEntity;
    private int _aliveDotCounter;
    private BlobAssetReference<GameSettingsBlob> _settings;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AliveDotCounterComponent>();
        state.RequireForUpdate<LevelsBlobComponent>();
        state.RequireForUpdate<InitShootedGhostBufferElement>();
    }

    public void OnUpdate(ref SystemState state)
    {
        _levels = SystemAPI.GetSingleton<LevelsBlobComponent>().Blob;
        _elapsedTime = SystemAPI.Time.ElapsedTime;

        _shootBuffer = SystemAPI.GetSingletonBuffer<InitShootedGhostBufferElement>();

        _ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
        _gameEntity = SystemAPI.GetSingletonEntity<SpawnTimeComponent>();

        _aliveDotCounter = SystemAPI.GetSingleton<AliveDotCounterComponent>().AliveDotCounter;
        _settings = SystemAPI.GetSingleton<GameSettingsBlobComponent>().Blob;

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (shoot, shooterTransform, shooterLevelComponent, team, direction) in SystemAPI.Query<
                     RefRW<ShootTimeComponent>,
                     RefRO<LocalTransform>,
                     RefRO<LevelComponent>,
                     RefRO<TeamComponent>,
                     RefRO<DirectionComponent>>())
        {
            ExecuteShootLogic(
                _elapsedTime,
                ref _levels,
                ref _shootBuffer,
                ref shoot.ValueRW,
                in shooterTransform.ValueRO,
                in shooterLevelComponent.ValueRO,
                in team.ValueRO,
                in direction.ValueRO
            );
        }

        ShootEventLogic(_ecb, _gameEntity, in _shootBuffer, _aliveDotCounter, ref _settings);
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new ShootJob
        {
            Time = _elapsedTime,
            Levels = _levels,
            Buffer = _shootBuffer
        };

        handle = job.Schedule(handle);

        var shootEventJob = new ShootEventJob
        {
            ECB = _ecb,
            GameEntity = _gameEntity,
            Buffer = _shootBuffer,
            AliveDotCounter = _aliveDotCounter,
            GameSettings = _settings
        };
        handle = shootEventJob.Schedule(handle);

        return handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShootEventLogic(EntityCommandBuffer.ParallelWriter ecb, Entity gameEntity, in DynamicBuffer<InitShootedGhostBufferElement> buffer, int aliveDotCounter, ref BlobAssetReference<GameSettingsBlob> gameSettings)
    {
        if (buffer.Length == 0) return;
        if (gameSettings.Value.MaxDots > aliveDotCounter)
        {
            ecb.AddComponent<GhostRequestEventFlag>(0, gameEntity);
        }
    }

    [BurstCompile]
    public partial struct ShootJob : IJobEntity
    {
        public double Time;
        [ReadOnly] public BlobAssetReference<LevelsBlob> Levels;
        // Todo removed these disables and write in a parallel writer list instead or chain dependencies
        [NativeDisableParallelForRestriction] public DynamicBuffer<InitShootedGhostBufferElement> Buffer;

        public void Execute(
            ref ShootTimeComponent shoot,
            in LocalTransform shooterTransform,
            in LevelComponent shooterLevelComponent,
            in TeamComponent team,
            in DirectionComponent direction)
        {
            ExecuteShootLogic(
                Time,
                ref Levels,
                ref Buffer,
                ref shoot,
                in shooterTransform,
                in shooterLevelComponent,
                in team,
                in direction
            );
        }
    }

    //write the Ijob that just calls ShootEventLogic
    [BurstCompile]
    public partial struct ShootEventJob : IJob
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        public Entity GameEntity;
        [ReadOnly] public DynamicBuffer<InitShootedGhostBufferElement> Buffer;
        public int AliveDotCounter;
        [ReadOnly] public BlobAssetReference<GameSettingsBlob> GameSettings;
        
        public void Execute()
        {
            ShootEventLogic(ECB, GameEntity, in Buffer, AliveDotCounter, ref GameSettings);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteShootLogic(
        double time,
        ref BlobAssetReference<LevelsBlob> levels,
        ref DynamicBuffer<InitShootedGhostBufferElement> buffer,
        ref ShootTimeComponent shoot,
        in LocalTransform shooterTransform,
        in LevelComponent shooterLevelComponent,
        in TeamComponent team,
        in DirectionComponent direction)
    {
        if (time < shoot.NextAllowedShootTime) return;

        ref var shooterLevel = ref levels.Value.Get(shooterLevelComponent.Index);
        if (!shooterLevel.ShootLevel.IsValid) return;

        ref var shootedDotLevel = ref shooterLevel.ShootLevel.Value;
        float3 forward = math.forward(shooterTransform.Rotation);
        var radiusShooter = DotHelper.GetHalfScale(shooterLevel.Size);
        var radiusShooted = DotHelper.GetHalfScale(shootedDotLevel.Size);
        float3 position = shooterTransform.Position + forward * (radiusShooter + radiusShooted + 0.2f);

        shoot.NextAllowedShootTime = (float)time + 1f / 3f;

        buffer.Add(new InitShootedGhostBufferElement
        {
            LevelIndex = shootedDotLevel.Index,
            Strategy = Strategy.InDirection,
            Position = position,
            Color = team.Color,
            Direction = forward
        });
    }
}