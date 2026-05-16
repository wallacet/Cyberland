using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Per-spawn bookkeeping for logical-actor parent links and post-spawn transform wiring.
/// </summary>
internal sealed class SceneSpawnSession
{
    public List<(EntityId Child, string ParentLogicalId)> PendingParentLinks { get; } = new();

    public Dictionary<string, EntityId> LogicalIdToEntity { get; } = new(StringComparer.Ordinal);
}
