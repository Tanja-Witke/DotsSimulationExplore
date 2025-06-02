//ToDo reenable the DotAuthoring
/*using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class DotAuthoring : MonoBehaviour
{
    public int Level = 3;

    [Tooltip("Movement Strategy")]
    public Strategy Strategy = Strategy.Random;

    [Tooltip("Team color")]
    public Team Team = Team.Red;
}

public class DotBaker : Baker<DotAuthoring>
{
    public override void Bake(DotAuthoring authoring)
    {
        var dotEntity = GetEntity(TransformUsageFlags.Dynamic);

        var eventEntity = CreateAdditionalEntity(TransformUsageFlags.None);

        AddComponent(eventEntity, new DotInitComponent
        {
            LevelNumber = authoring.Level,
            Strategy = authoring.Strategy,
            Color = DotHelper.GetColor(authoring.Team), 
            Position = authoring.transform.position,
            Direction = float3.zero,
            DotEntity = dotEntity
        });
    }
}*/