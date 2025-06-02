using Unity.Entities;
using Unity.Mathematics;

public struct LevelsBlob
{
    public BlobArray<Level> Levels;
    //public int LevelCount;

    public ref Level Get(int index)
    {
        index = math.clamp(index, 0, Levels.Length - 1);
        return ref Levels[index];
    }

    // For now there are no xp ranges for levels
    public ref Level GetFromXP(int xp)
    {
        return ref Get(xp);
    }
}