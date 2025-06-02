using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public static class DotHelper
{
    public static float GetScale(int volume)
    {
        return 2f * GetHalfScale(volume);
    }

    public static float GetHalfScale(int volume)
    {
        return  math.pow((3f * volume) / (4f * math.PI), 1f / 3f);
    }

    public static float4 GetColor(Team team)
    {
        return team switch
        {
            Team.Red => new float4(1, 0, 0, 1),
            Team.Green => new float4(0, 1, 0, 1),
            Team.Blue => new float4(0, 0, 1, 1),
            _ => new float4(0, 0, 0, 1) // Black
        };
    }

    public static void Disable(EntityCommandBuffer.ParallelWriter ecb, Entity dotEntity, RefRW<AliveDotCounterComponent> counter)
    {
        ecb.AddComponent<Disabled>(0, dotEntity);
        counter.ValueRW.AliveDotCounter -= 1;
    }
}
