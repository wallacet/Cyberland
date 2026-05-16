namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Snapshot of a scene instance for diagnostics and mod UI.
/// </summary>
public readonly record struct SceneStatus(
    SceneInstanceId Id,
    SceneRuntimeState State,
    int Priority,
    string? ScenePath,
    string? LastError);
