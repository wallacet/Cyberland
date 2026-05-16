namespace Cyberland.Engine.RuntimeScenes;

/// <summary>Outcome of <see cref="ISceneRuntime.SpawnIntoWorldAsync"/>.</summary>
public readonly struct SceneSpawnResult
{
    /// <summary>When true, all entities from the document were committed.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Human-readable failure reason when <see cref="Succeeded"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Entities created in the target world.</summary>
    public int EntitiesSpawned { get; init; }
}
