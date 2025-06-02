using UnityEngine;
using Unity.Entities;
using Unity.Transforms;

public class FollowPlayer : ECSBridgeMonoBehaviour
{
    override protected void InternalUpdate()
    {
        var playerTransform = EntityManager.GetComponentData<LocalToWorld>(PlayerEntity);
        transform.position = (Vector3)playerTransform.Position;
        transform.rotation = (Quaternion)playerTransform.Rotation;
    }
}
