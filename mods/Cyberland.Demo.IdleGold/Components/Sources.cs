namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Unlock and level state for the four income sources (see <see cref="SourceId"/>).</summary>
public struct Sources : Cyberland.Engine.Core.Ecs.IComponent
{
    public SourceRow VillageBeg;
    public SourceRow ForestForage;
    public SourceRow CaveExplore;
    public SourceRow RoadToll;
}

/// <summary>One gatherable source; <see cref="Level"/> is ≥ 1 when <see cref="Unlocked"/>.</summary>
public struct SourceRow
{
    public bool Unlocked;
    public int Level;
}
