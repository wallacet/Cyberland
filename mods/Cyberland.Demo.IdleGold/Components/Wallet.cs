namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Currency accumulated from passive sources and Luck procs.</summary>
public struct Wallet : Cyberland.Engine.Core.Ecs.IComponent
{
    public double Gold;
    public double LifetimeEarned;
}
