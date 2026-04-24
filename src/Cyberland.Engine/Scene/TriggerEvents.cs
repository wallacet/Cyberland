using System.Collections.Generic;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Per-entity trigger event buffer filled each fixed tick by <see cref="Systems.TriggerSystem"/>.
/// </summary>
/// <remarks>
/// The system clears and repopulates <see cref="Events"/> every fixed tick. Mod systems that consume trigger transitions should
/// read this buffer in fixed update after the trigger system has run.
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Trigger"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>). World placement still comes from <see cref="Transform"/> when present;
/// <see cref="Systems.TriggerSystem"/> skips triggers that have no transform row.
/// </remarks>
[RequiresComponent<Trigger>]
public struct TriggerEvents : IComponent
{
    /// <summary>
    /// Trigger transition events for the current fixed tick.
    /// </summary>
    public List<TriggerEvent>? Events;
}
