namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Budgets for a single <see cref="ISceneRuntime.PumpAsync"/> call.
/// </summary>
public sealed class SceneLoadPumpOptions
{
    /// <summary>
    /// Maximum wall time to spend in this pump slice (best-effort).
    /// Values of <see cref="TimeSpan.Zero"/> or less disable the wall-time cap (same as leaving this unset).
    /// </summary>
    public TimeSpan? MaxElapsed { get; init; }

    /// <summary>Maximum UTF-8 bytes to read from the scene file in this slice (when streaming reads are used).</summary>
    public int? MaxReadBytes { get; init; }

    /// <summary>Maximum entities to commit from the staged spawn list in this slice.</summary>
    public int? MaxEntitiesToCommit { get; init; }

    /// <summary>Maximum asset queue jobs to dequeue in this slice (see <see cref="SceneAssetRequestQueue"/>).</summary>
    public int? MaxAssetJobs { get; init; }

    /// <summary>Maximum bytes asset jobs may decode in aggregate this slice.</summary>
    public int? MaxAssetDecodeBytes { get; init; }
}
