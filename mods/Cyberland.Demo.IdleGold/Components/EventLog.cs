namespace Cyberland.Demo.IdleGold.Components;

/// <summary>Append-only localized lines for the Log tab (reference-type payload lives outside ECS archetype norms but keeps one session row simple).</summary>
public struct EventLog : Cyberland.Engine.Core.Ecs.IComponent
{
    public List<string>? Lines;

    /// <summary>Bumped when lines are appended; HUD can skip rebuilding log body text when unchanged.</summary>
    public int ContentRevision;
}
