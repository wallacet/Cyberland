namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Lifecycle state for a runtime scene instance (see plan state machine).
/// </summary>
public enum SceneRuntimeState
{
    /// <summary>World and scheduler allocated; load not started.</summary>
    Allocated,

    /// <summary>Scene file / assets are being resolved.</summary>
    Loading,

    /// <summary>Tick and render participate in the scene stack.</summary>
    Ready,

    /// <summary>Tearing down entities and resources.</summary>
    Unloading,

    /// <summary>Terminal: resources released.</summary>
    Unloaded,

    /// <summary>Terminal: load failed; no partial live state.</summary>
    Failed,

    /// <summary>Terminal: load cancelled before ready.</summary>
    Cancelled
}
