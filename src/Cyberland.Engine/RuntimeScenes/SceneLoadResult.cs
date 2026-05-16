namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Summary counters returned from <see cref="ISceneRuntime.PumpAsync"/>.
/// </summary>
public sealed class SceneLoadResult
{
    /// <summary>Jobs started this slice (asset + internal).</summary>
    public int JobsStarted { get; init; }

    /// <summary>Jobs completed this slice.</summary>
    public int JobsCompleted { get; init; }

    /// <summary>Bytes decoded from assets or scene text this slice.</summary>
    public int BytesDecoded { get; init; }

    /// <summary>Entities committed to the live world this slice.</summary>
    public int EntitiesCommitted { get; init; }

    /// <summary>Non-fatal warnings accumulated this slice.</summary>
    public int Warnings { get; init; }

    /// <summary>Whether the scene reached <see cref="SceneRuntimeState.Ready"/> this slice.</summary>
    public bool Completed { get; init; }

    /// <summary>Whether a fatal error was recorded (scene moves to <see cref="SceneRuntimeState.Failed"/>).</summary>
    public bool Failed { get; init; }

    /// <summary>Optional error message when <see cref="Failed"/> is true.</summary>
    public string? ErrorMessage { get; init; }
}
