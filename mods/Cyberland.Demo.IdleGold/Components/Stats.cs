namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Trainable stat levels affecting income multipliers.</summary>
public struct Stats : Cyberland.Engine.Core.Ecs.IComponent
{
    public int Might;
    public int Cunning;
    public int Resolve;
    public int Luck;
}
