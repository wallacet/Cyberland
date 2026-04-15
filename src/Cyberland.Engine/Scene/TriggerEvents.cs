using System.Collections.Generic;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Per-entity trigger event buffer filled each fixed tick by <see cref="Systems.TriggerSystem"/>.
/// </summary>
/// <remarks>
/// The system clears and repopulates <see cref="Events"/> every fixed tick. Mod systems that consume trigger transitions should
/// read this buffer in fixed update after the trigger system has run.
/// </remarks>
public struct TriggerEvents
{
    /// <summary>
    /// Trigger transition events for the current fixed tick.
    /// </summary>
    public List<TriggerEvent>? Events;
}
