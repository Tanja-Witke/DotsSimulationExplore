using Unity.Entities;

[InternalBufferCapacity(100)]
public struct DeadDotBufferElement : IBufferElementData
{
    public Entity Dot;
}