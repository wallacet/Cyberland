using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Stable logical identity for save, quests, and entity lift bookkeeping; orthogonal to <see cref="EntityId"/>.
/// </summary>
public struct LogicalActorId : IComponent
{
    /// <summary>Stable GUID string (normalized).</summary>
    public string Guid;

    /// <summary>Whether <see cref="Guid"/> is assigned.</summary>
    public bool HasValue => !string.IsNullOrEmpty(Guid);
}
