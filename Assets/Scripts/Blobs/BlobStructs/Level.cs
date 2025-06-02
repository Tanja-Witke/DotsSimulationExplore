using Unity.Entities;

public struct Level
{
    public int Index;
    public int Size;
    public float Speed;
    public BlobPtr<Level> ShootLevel;
}