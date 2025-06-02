#if DEBUG_DISPLAY_COUNTED_DOTS
using Unity.Entities;
using UnityEngine;


[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct CounterSystem : ISystem
{
    private int _counter;
    public void OnCreate(ref SystemState state)
    {
        _counter = 0;
    }
    public void OnUpdate(ref SystemState state)
    {
        foreach (var counter in SystemAPI.Query<RefRO<AliveDotCounterComponent>>())
        {
            var currentDotsCount = counter.ValueRO.AliveDotCounter;
            if (currentDotsCount > _counter)
            {
                _counter = currentDotsCount;
                Debug.Log($"MaxDotsCount: {_counter}");
            }
        }
    }
}
#endif
