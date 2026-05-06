namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Deterministic RNG seed for Luck; accumulator avoids per-frame proc bias.</summary>
public struct RngState : Cyberland.Engine.Core.Ecs.IComponent
{
    public ulong State;
    public float LuckAccumulatorSec;
}
