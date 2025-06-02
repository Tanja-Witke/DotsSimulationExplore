using Unity.Entities;

[InternalBufferCapacity(100)]
public struct CollisionEventBufferElement : IBufferElementData
{
    public Entity Source;
    public Entity Target;
}