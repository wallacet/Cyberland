namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Raised when a scene transitions between <see cref="SceneRuntimeState"/> values.
/// </summary>
public sealed class SceneStateChangedEventArgs : EventArgs
{
    /// <summary>Creates event args for a state transition.</summary>
    public SceneStateChangedEventArgs(SceneInstanceId id, SceneRuntimeState previous, SceneRuntimeState current)
    {
        Id = id;
        Previous = previous;
        Current = current;
    }

    /// <summary>Scene instance that changed.</summary>
    public SceneInstanceId Id { get; }

    /// <summary>Previous state.</summary>
    public SceneRuntimeState Previous { get; }

    /// <summary>New state.</summary>
    public SceneRuntimeState Current { get; }
}
