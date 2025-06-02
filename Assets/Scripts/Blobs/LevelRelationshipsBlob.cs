using Unity.Entities;

public struct LevelRelationshipsBlob
{
    public BlobArray<LevelRelationship> LevelRelationships;
    public int LevelCount;

    public (int xpImpact, int colorImpact) Get(int levelAIndex, int levelBIndex, bool sameTeam) 
    {
        ref var relationship = ref LevelRelationships[GetIndex(levelAIndex, levelBIndex, sameTeam)];
        return (relationship.XPImpact, relationship.ColorImpact); 
    }

    private int GetIndex(int sourceIndex, int targetIndex, bool sameTeam)
    {
        return sourceIndex * LevelCount * 2
             + targetIndex * 2
             + (sameTeam ? 0 : 1);
    }
}