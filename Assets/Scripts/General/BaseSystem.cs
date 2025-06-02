using Unity.Entities;
using Unity.Jobs;


public static class BaseSystem
{
    private delegate JobHandle DotsUpdateJobDelegate(JobHandle handle, ref SystemState state);
    private delegate JobHandle DotsUpdateDelegate(ref SystemState state);

    public static void Update<TSubSystem>(ref TSubSystem subSystem, ref SystemState state) where TSubSystem : struct, ISubSystem
    {
#if DEBUG_SYSTEM_ORDER || DEBUG_SYSTEM_DURATION
        var name = typeof(TSubSystem).Name;
        TimingTrackerSystem.Start(name);
#endif
        subSystem.Update(ref state);
#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_DURATION
        TimingTrackerSystem.Stop(name);
#endif
    }

    public static void OnUpdate<TSubSystem>(ref TSubSystem subSystem, ref SystemState state) where TSubSystem : struct, ISubSystem
    {
#if UPDATE_INSTEAD_OF_SCHEDULE
        Update(ref subSystem, ref state);
        return;
#endif
        var dependencies = state.Dependency;
        var handle = Schedule(dependencies, ref state, subSystem.Schedule, typeof(TSubSystem).Name);
        state.Dependency = handle;
    }

    private static JobHandle Schedule(
        JobHandle handle, ref SystemState state,
        DotsUpdateJobDelegate updateMethod, string name)
    {
#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
        var startJob = new StartJob
        {
            SystemName = name
        };

        handle = startJob.Schedule(handle);

        var stopwatch = Stopwatch.StartNew();
#endif

        handle = updateMethod(handle, ref state);

#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
        stopwatch.Stop();
        var stopJob = new StopJob
        {
            SystemName = name,
            AdditionalTime = stopwatch.Elapsed.TotalMilliseconds
        };

        handle = stopJob.Schedule(handle);
#endif

        return handle;
    }
}

public interface ISubSystem : ISystem
{
    public JobHandle Schedule(JobHandle dependencies, ref SystemState state) { return dependencies; }
    public void Update(ref SystemState state) { }
}