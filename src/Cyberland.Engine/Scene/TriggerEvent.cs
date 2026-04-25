using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// One trigger transition event emitted onto an entity's <see cref="TriggerEvents"/> buffer.
/// </summary>
/// <remarks>
/// Events are emitted in fixed update by <see cref="Systems.TriggerSystem"/> and represent transitions against the prior fixed tick.
/// Event ordering inside a buffer is not guaranteed when trigger processing runs in parallel.
/// </remarks>
public struct TriggerEvent
{
    /// <summary>
    /// The entity that received this event.
    /// </summary>
    public EntityId Self;

    /// <summary>
    /// The other trigger entity involved in this transition.
    /// </summary>
    public EntityId Other;

    /// <summary>
    /// Enter/stay/exit transition kind for this entity against <see cref="Other"/>.
    /// </summary>
    public TriggerEventKind Kind;

    /// <summary>
    /// The other entity's trigger layer bit used when this event was generated.
    /// </summary>
    public uint OtherLayerMask;
}
