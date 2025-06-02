using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Unity.Transforms;

public class InputSystemBridge : ECSBridgeMonoBehaviour
{
    [SerializeField] private PlayerControls InputActions;

    private float _yaw;

    private void OnEnable()
    {
        if (InputActions == null)
            InputActions = new PlayerControls();

        InputActions.Enable();
    }

    private void OnDisable()
    {
        if (InputActions != null)
            InputActions.Disable();
    }


    override protected void InternalUpdate()
    {

        if (!EntityManager.HasComponent<LocalTransform>(PlayerEntity))
            return;

        // Get and update rotation first
        var playerTransform = EntityManager.GetComponentData<LocalTransform>(PlayerEntity);
        float2 lookInput = InputActions.Player_Map.Look.ReadValue<Vector2>();
        _yaw += lookInput.x;

        playerTransform.Rotation = quaternion.Euler(0, math.radians(_yaw), 0);
        EntityManager.SetComponentData(PlayerEntity, playerTransform);

        // Now use the updated rotation to transform movement input
        float2 input = InputActions.Player_Map.Move.ReadValue<Vector2>();
        float3 moveInput = new float3(input.x, 0, input.y);
        float3 direction = math.normalizesafe(math.mul(playerTransform.Rotation, moveInput));

        // Apply to movement component
        if (EntityManager.HasComponent<DirectionComponent>(PlayerEntity))
        {
            var dir = EntityManager.GetComponentData<DirectionComponent>(PlayerEntity);
            dir.Direction = direction;
            EntityManager.SetComponentData(PlayerEntity, dir);
        }
    }
}
