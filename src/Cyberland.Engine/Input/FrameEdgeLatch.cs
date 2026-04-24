using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Input;

/// <summary>
/// Optional helper for one-shot actions that are <strong>set</strong> from <see cref="Core.Ecs.IEarlyUpdate"/> (e.g. menu confirm
/// on key edge) and <strong>consumed</strong> from <see cref="Core.Ecs.IFixedUpdate"/> with <see cref="TryConsume"/>.
/// </summary>
/// <remarks>
/// <see cref="Core.Tasks.SystemScheduler.RunFrame(Cyberland.Engine.Core.Ecs.World, float)"/> may run <see cref="Core.Ecs.IFixedUpdate"/> multiple times per frame when
/// the wall-clock step spans more than one fixed tick. Do not wipe entire input components in fixed after reading
/// <strong>held</strong> state — that breaks later substeps. For held movement, reset in early each frame; use this type only
/// for edge-triggered flags.
/// </remarks>
public struct FrameEdgeLatch : IComponent
{
    private bool _armed;

    /// <summary>Sets the latch; typically called from early when an edge condition is true.</summary>
    public void Arm() => _armed = true;

    /// <summary>Returns <see langword="true"/> once then clears the latch.</summary>
    public bool TryConsume()
    {
        if (!_armed)
            return false;
        _armed = false;
        return true;
    }

    /// <summary>Whether the latch is still armed (does not clear).</summary>
    public readonly bool IsArmed => _armed;
}
