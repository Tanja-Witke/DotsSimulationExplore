using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Unity.Transforms;

public abstract class ECSBridgeMonoBehaviour : MonoBehaviour
{
    protected EntityManager EntityManager;
    protected Entity PlayerEntity;

    private void Start()
    {
        EntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void Update()
    {
        if (PlayerEntity == Entity.Null && !TryGetPlayer(out PlayerEntity))
            return;

        InternalUpdate();
    }

    abstract protected void InternalUpdate();

    private bool TryGetPlayer(out Entity playerEntity)
    {
        var query = EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<PlayerTag>(),
            ComponentType.ReadWrite<DirectionComponent>());

        if (!query.IsEmptyIgnoreFilter)
        {
            playerEntity = query.GetSingletonEntity();
            return true;
        }
        else
        {
            playerEntity = Entity.Null;
        }
        return false;
    }
}
