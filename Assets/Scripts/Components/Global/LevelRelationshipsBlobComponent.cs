using Unity.Entities;

public struct LevelRelationshipsBlobComponent : IComponentData
{
    public BlobAssetReference<LevelRelationshipsBlob> Blob;
}
