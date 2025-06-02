// ToDo optimize performance with 100+ entities

using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public class GameAuthoring : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float SecondsBetweenWaves = 5f;
    public int DotMaxSpawnLevel = 2;
    public int DotsPerWave = 3;
    public int MaxDots = 50;
    //ToDo add LOD again
    /*public float LOD01Distance = 5f;
    public float LOD02Distance = 10f;*/
    public float FireRate = 2f;
    public GameObject DotPrefab;
    public GameObject GhostPrefab;
    //public PhysicsMaterialTemplate TriggerMaterial;

    [Header("Level Settings")]
    
    public int LevelCount = 30;
    public int ShootLevelPercent = 25;
    public float StartSpeed = 20f;
    public float MinSpeed = 5f;
}

public class GameAuthoringBaker : Baker<GameAuthoring>
{
    private bool _initialized = false;

    public override void Bake(GameAuthoring authoring)
    {
        if (_initialized) return;

        var gameEntity = GetEntity(TransformUsageFlags.None);

        AddComponent(gameEntity, new GameTag());

        AddComponent(gameEntity, new AliveDotCounterComponent
        {
            AliveDotCounter = 0,
        });

        AddComponent(gameEntity, new RandomComponent
        {
            Value = new Unity.Mathematics.Random(12345)
        });

        AddComponent(gameEntity, new SpawnTimeComponent
        {
            NextAllowedTime = 0
        });

        var collisionEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddBuffer<CollisionEventBufferElement>(collisionEntity);

        var deadDotEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddBuffer<DeadDotBufferElement>(deadDotEntity);

        var shootedGhostEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddBuffer<InitShootedGhostBufferElement>(shootedGhostEntity);

        var spawnedGhostEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddBuffer<InitSpawnedGhostBufferElement>(spawnedGhostEntity);

        var removeDotEntity = CreateAdditionalEntity(TransformUsageFlags.None);
        AddBuffer<RemoveDotBufferElement>(removeDotEntity);


        AddComponent(gameEntity, new DotPrefabComponent
        {
            Prefab = GetEntity(authoring.DotPrefab, TransformUsageFlags.Dynamic)
        });

        AddComponent(gameEntity, new GhostPrefabComponent
        {
            Prefab = GetEntity(authoring.GhostPrefab, TransformUsageFlags.Dynamic)
        });

        //ToDo investigate how to share jobHandles between systems without using static data

        var settings = CreateSettingsBlob(authoring);
        var levels = CreateLevelsBlob(authoring);
        var levelRelationships = CreateLevelRelationshipsBlob(authoring, levels);

        AddBlobAsset(ref settings, out var _);
        AddBlobAsset(ref levels, out var _);
        AddBlobAsset(ref levelRelationships, out var _);

        AddComponent(gameEntity, new GameSettingsBlobComponent
        {
            Blob = settings
        });

        AddComponent(gameEntity, new LevelsBlobComponent
        {
            Blob = levels
        });

        AddComponent(gameEntity, new LevelRelationshipsBlobComponent
        {
            Blob = levelRelationships
        });

        _initialized = true;
    }

    private BlobAssetReference<GameSettingsBlob> CreateSettingsBlob(GameAuthoring authoring)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<GameSettingsBlob>();

        root.FireRate = authoring.FireRate;
        root.SecondsBetweenWaves = authoring.SecondsBetweenWaves;
        root.DotsPerWave = authoring.DotsPerWave;
        root.MaxDots = authoring.MaxDots;
        root.DotMaxSpawnLevel = math.min(authoring.LevelCount - 1, authoring.DotMaxSpawnLevel);

        var settingsBlob = builder.CreateBlobAssetReference<GameSettingsBlob>(Allocator.Persistent);
        builder.Dispose();
        return settingsBlob;
    }

    private BlobAssetReference<LevelsBlob> CreateLevelsBlob(GameAuthoring authoring)
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<LevelsBlob>();

        var levelCount = authoring.LevelCount;
        var levels = builder.Allocate(ref root.Levels, levelCount);

        float speed = authoring.StartSpeed;
        float speedStep = (speed - authoring.MinSpeed) / levelCount;
        int shootPercent = authoring.ShootLevelPercent;

        for (int i = 0; i < levelCount; i++)
        {
            int shootIndex = (int)math.round(i * shootPercent / 100f) - 1;

            ref var level = ref levels[i];
            level.Size = i + 1;
            level.Speed = speed;
            level.Index = i;
   
            if (shootIndex >= 0)
            {
                builder.SetPointer(ref level.ShootLevel, ref levels[shootIndex]);
            }
            else
            {
                level.ShootLevel = default;
            }

            speed -= speedStep;
        }

        var blob = builder.CreateBlobAssetReference<LevelsBlob>(Allocator.Persistent);
        builder.Dispose();
        return blob;
    }


    private BlobAssetReference<LevelRelationshipsBlob> CreateLevelRelationshipsBlob(GameAuthoring authoring, BlobAssetReference<LevelsBlob> levelsBlob)
    {
        ref var levels = ref levelsBlob.Value;
        int levelCount = authoring.LevelCount;
        int totalRelCount = levelCount * levelCount * 2;

        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<LevelRelationshipsBlob>();
        root.LevelCount = levelCount;

        var blobArray = builder.Allocate(ref root.LevelRelationships, totalRelCount);

        int index = 0;

        for (int sourceIndex = 0; sourceIndex < levelCount; sourceIndex++)
        {
            ref var source = ref levels.Get(sourceIndex);

            for (int targetIndex = 0; targetIndex < levelCount; targetIndex++)
            {
                ref var target = ref levels.Get(targetIndex);

                for (int s = 0; s < 2; s++)
                {
                    bool sameTeam = (s == 0);

                    (int xpImpact, int colorImpact) = GetImpactOnTarget(
                        sameTeam,
                        source.Size,
                        target.Size
                    );

                    blobArray[index++] = new LevelRelationship
                    {
                        XPImpact = xpImpact,
                        ColorImpact = colorImpact
                    };
                }
            }
        }

        var blob = builder.CreateBlobAssetReference<LevelRelationshipsBlob>(Allocator.Persistent);
        builder.Dispose();
        return blob;
    }


    private (int xpImpact, int colorImpact) GetImpactOnTarget(bool sameTeam, int sourceSize, int targetSize)
    {
        // If source and target are exactly the same size, minor color reward, no XP change
        if (sourceSize == targetSize)
        {
            return (0, sourceSize);
        }

        int xpImpact;
        int colorImpact;

        if (sameTeam)
        {
            if (sourceSize > targetSize)
            {
                // Source absorbs Target (friendly absorb)
                xpImpact = -sourceSize;
                colorImpact = 0;
            }
            else
            {
                // Source gets absorbed by Target (friendly absorb)
                xpImpact = sourceSize;
                colorImpact = sourceSize;
            }
        }
        else
        {
            // Enemies damage each other
            xpImpact = -sourceSize;
            colorImpact = sourceSize;
        }

        return (xpImpact, colorImpact);
    }
}