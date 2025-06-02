using Unity.Entities;

[InternalBufferCapacity(100)]
public struct RemoveDotBufferElement : IBufferElementData
{
    public Entity Dot;
}