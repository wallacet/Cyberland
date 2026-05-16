namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Options for <see cref="ISceneRuntime.SpawnIntoWorldAsync"/> (one-shot root-world authoring).
/// </summary>
public sealed class SceneSpawnOptions
{
    /// <summary>When false, unknown component <c>type</c> strings fail the spawn.</summary>
    public bool AllowUnknownComponentTypes { get; init; }
}
