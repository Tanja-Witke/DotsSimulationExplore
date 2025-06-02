using Unity.Burst;
using Unity.Entities;


[BurstCompile]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct EventSystem : ISubSystem
{
    public void OnCreate(ref SystemState state)
    {
        EntityQuery query = state.GetEntityQuery(new EntityQueryDesc
        {
            Any = new ComponentType[]
            {
                typeof(SpawnRequestEventFlag),
                typeof(KillRequestEventFlag),
                typeof(ImpactEventFlag),
                typeof(CollisionEventFlag),
                typeof(GhostRequestEventFlag)

        }
        });

        state.RequireAnyForUpdate(query);
    }

    public void OnUpdate(ref SystemState state)
    {
        //get beginfixedstep ecb
        
        var ecb = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);


        var gameEntity = SystemAPI.GetSingletonEntity<GameTag>();

        ecb.RemoveComponent<SpawnRequestEventFlag>(gameEntity);
        ecb.RemoveComponent<KillRequestEventFlag>(gameEntity);
        ecb.RemoveComponent<ImpactEventFlag>(gameEntity);
        ecb.RemoveComponent<CollisionEventFlag>(gameEntity);
        ecb.RemoveComponent<GhostRequestEventFlag>(gameEntity);
    }
}