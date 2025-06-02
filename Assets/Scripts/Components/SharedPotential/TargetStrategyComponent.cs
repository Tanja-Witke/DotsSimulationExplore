using Unity.Entities;

// ToDo investigate: depending on how many chunks there will be, this could be another shared component to seperate useful chunks. But since it is changing a lot, copying this around might be more expensive than it would save performance
public struct TargetStrategyComponent : IComponentData
{
    public Strategy Strategy;
}