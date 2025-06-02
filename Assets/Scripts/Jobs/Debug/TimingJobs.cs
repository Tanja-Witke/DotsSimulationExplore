#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

[BurstCompile]
public struct StartJob : IJob
{
    public FixedString64Bytes SystemName;

    public void Execute()
    {
#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
        TimingTrackerSystem.Start(SystemName.ToString());
#endif
    }
}

[BurstCompile]
public struct StopJob : IJob
{
    public FixedString64Bytes SystemName;
    public double AdditionalTime;

    public void Execute()
    {

#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
        TimingTrackerSystem.Stop(SystemName.ToString(), AdditionalTime);
#endif
    }
}
#endif
