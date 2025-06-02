
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ColorSystem : ISubSystem
{
    private const int _sections = 5;
    private const float _step = 1.0f / (_sections - 1);

    private EntityCommandBuffer _ecb;

    [BurstCompile]
    public partial struct ColorJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute([EntityIndexInQuery] int index, Entity dotEntity, in TeamComponent team, [WithChangeFilter] in RGBComponent rgb, in URPMaterialPropertyBaseColor color)
        {
            ColorLogic(index, dotEntity, ECB, in team, in color, rgb.Red, rgb.Green, rgb.Blue);
        }
    }

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<DotTag>();
        state.RequireForUpdate<URPMaterialPropertyBaseColor>();
        state.RequireForUpdate<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();

        state.RequireForUpdate<ImpactEventFlag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        _ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        BaseSystem.OnUpdate(ref this, ref state);
    }

    public void Update(ref SystemState state)
    {
        foreach (var (team, rgb, color, dotEntity) in SystemAPI.Query<
            RefRO<TeamComponent>,
            RefRO<RGBComponent>,
            RefRO<URPMaterialPropertyBaseColor>>()
            .WithEntityAccess()
            .WithChangeFilter<RGBComponent>())
        {
            ColorLogic(0, dotEntity, _ecb.AsParallelWriter(), in team.ValueRO, in color.ValueRO, rgb.ValueRO.Red, rgb.ValueRO.Green, rgb.ValueRO.Blue);
        }
    }

    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ColorLogic(int index, Entity dotEntity, EntityCommandBuffer.ParallelWriter ecb, in TeamComponent team, in URPMaterialPropertyBaseColor color, float red, float green, float blue)
    {
        float maxValue = math.max(red, math.max(green, blue));

        if (maxValue <= 0f) return;

        float rNorm = red / maxValue;
        float gNorm = green / maxValue;
        float bNorm = blue / maxValue;

        float rSection = math.round(rNorm * (_sections - 1)) * _step;
        float gSection = math.round(gNorm * (_sections - 1)) * _step;
        float bSection = math.round(bNorm * (_sections - 1)) * _step;

        if (rSection >= 1f && gSection >= 1f && bSection >= 1f)
        {
            if (rSection >= gSection && rSection >= bSection)
                rSection -= _step;
            else if (gSection >= rSection && gSection >= bSection)
                gSection -= _step;
            else
                bSection -= _step;
        }

        var newColor = new float4(rSection, gSection, bSection, 1f);

        ecb.AddComponent(index, dotEntity, new TeamComponent { Color = newColor });
        ecb.SetComponent(index, dotEntity, new URPMaterialPropertyBaseColor { Value = newColor });
    }

    public JobHandle Schedule(JobHandle handle, ref SystemState state)
    {
        var job = new ColorJob { ECB = _ecb.AsParallelWriter() };
        handle = job.ScheduleParallel(handle);
        return handle;
    }
}
