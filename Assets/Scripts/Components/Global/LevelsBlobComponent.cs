using Unity.Entities;

public struct LevelsBlobComponent : IComponentData
{
    public BlobAssetReference<LevelsBlob> Blob;
}
