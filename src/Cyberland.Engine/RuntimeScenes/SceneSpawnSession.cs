using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Per-spawn bookkeeping for logical-actor parent links and post-spawn transform wiring.
/// </summary>
internal sealed class SceneSpawnSession
{
    public List<(EntityId Child, string ParentLogicalId)> PendingParentLinks { get; } = new();

    public List<(EntityId Camera, string TargetLogicalId)> PendingCameraFollowTargets { get; } = new();

    public Dictionary<string, EntityId> LogicalIdToEntity { get; } = new(StringComparer.Ordinal);

    /// <summary>Entities with <c>ui-document-root</c> + <c>uiPath</c> to attach after spawn.</summary>
    public List<(EntityId Entity, string UiPath)> PendingUiDocuments { get; } = new();
}
