using Unity.Entities;

public struct GameSettingsBlobComponent : IComponentData
{
    public BlobAssetReference<GameSettingsBlob> Blob;
}
