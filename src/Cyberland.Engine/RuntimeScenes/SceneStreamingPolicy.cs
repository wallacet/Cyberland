namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Documents how open-world streaming maps onto <see cref="ISceneRuntime"/> without extra engine types on the hot path.
/// </summary>
/// <remarks>
/// <para><b>Cell grid (heavy isolation):</b> each active cell can be one additive <see cref="SceneInstanceId"/> with its own
/// <see cref="Core.Ecs.World"/> + <see cref="Core.Tasks.SystemScheduler"/>. A root-scene system tracks player cell coordinates,
/// calls <see cref="ISceneRuntime.BeginLoad"/> for neighbors, and <see cref="ISceneRuntime.RequestUnload"/> for distant cells.
/// Cross-cell persistence uses <see cref="ISceneRuntime.TryLiftSubtree"/> (or entity sets) into the long-lived root world before unload.</para>
/// <para><b>Trigger-driven (lighter orchestration):</b> gameplay stays in the root world; <see cref="Scene.Trigger"/> volumes or
/// scripted events call the same <see cref="ISceneRuntime"/> APIs to spawn overlay worlds (loading UI, instanced interiors) or
/// file-backed cells when you want hard ECS isolation for a slice of content.</para>
/// <para>Both patterns share one rule: <see cref="ISceneRuntime.PumpAsync"/> is advanced by the host once per frame—mods enqueue
/// loads and observe <see cref="Hosting.GameHostServices.InGameLoadProgress"/> / scene status; they must not spin independent pump loops.</para>
/// </remarks>
public static class SceneStreamingPolicy
{
    /// <summary>Stable name for diagnostics / tests (keeps the type referenced).</summary>
    public const string Anchor = nameof(SceneStreamingPolicy);
}
