namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Describes a new additive runtime scene load request.
/// </summary>
public sealed class SceneLoadDescriptor
{
    /// <summary>Virtual path to scene JSON in the layered VFS (e.g. <c>Scenes/level01.json</c>; mod content mounts at each mod's <c>Content/</c> root).</summary>
    public required string ScenePath { get; init; }

    /// <summary>Lower runs first for tick and render ordering; ties use insertion order.</summary>
    public int Priority { get; init; }

    /// <summary>When true, unknown component type strings do not fail the load (debug/dev only).</summary>
    public bool AllowUnknownComponentTypes { get; init; }

    /// <summary>Optional cap on entities spawned per <see cref="ISceneRuntime.PumpAsync"/> call (default from runtime options).</summary>
    public int? MaxEntitiesPerPump { get; init; }
}
