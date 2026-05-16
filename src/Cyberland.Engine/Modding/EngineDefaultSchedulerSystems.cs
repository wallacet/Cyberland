using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene.Systems;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Registers the stock engine ECS systems that ship with Cyberland.
/// </summary>
/// <remarks>
/// <para>
/// The host composes these registrations through shipped engine mods so modpacks can disable or replace
/// systems with normal <see cref="ModLoadContext"/> controls (<see cref="ModLoadContext.SetSystemEnabled"/>,
/// <see cref="ModLoadContext.TryUnregister"/>, and re-registration by logical id).
/// </para>
/// <para>
/// Registration is split into two phases:
/// </para>
/// <list type="bullet">
/// <item><description><see cref="RegisterBeforeGameplayMods"/>: simulation prep that runs before gameplay mods.</description></item>
/// <item><description><see cref="RegisterAfterGameplayMods"/>: camera/layout/render/UI systems that run after gameplay mods.</description></item>
/// </list>
/// </remarks>
public static class EngineDefaultSchedulerSystems
{
    /// <summary>
    /// Registers stock early engine systems onto an arbitrary <see cref="SystemScheduler"/> (used for additive runtime scenes).
    /// </summary>
    public static void RegisterStockEarlySystems(SystemScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        scheduler.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
        scheduler.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
        scheduler.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem());
    }

    /// <summary>
    /// Registers stock late engine systems onto an arbitrary <see cref="SystemScheduler"/> (used for additive runtime scenes).
    /// </summary>
    public static void RegisterStockLateSystems(SystemScheduler scheduler, GameHostServices host, Action? afterEachStep = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(host);

        scheduler.RegisterParallel("cyberland.engine/camera-follow", new CameraFollowSystem());
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/trigger", new TriggerSystem());
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/camera-submit", new CameraSubmitSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/camera-runtime-state", new CameraRuntimeStateSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/viewport-layout", new ViewportAnchorSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/lighting-ambient", new AmbientLightSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/lighting-directional", new DirectionalLightSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/lighting-spot", new SpotLightSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/lighting-point", new PointLightSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/global-post-process", new GlobalPostProcessSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/post-process-volumes", new PostProcessVolumeSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/tilemap-render", new TilemapRenderSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/sprite-localized-assets", new SpriteLocalizedAssetSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/sprite-render", new SpriteRenderSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/particle-render", new ParticleRenderSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/text-staging", new TextStagingSystem());
        afterEachStep?.Invoke();
        scheduler.RegisterParallel("cyberland.engine/text-render", new TextRenderSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/ui-document-frame", new UiDocumentFrameSystem(host));
        afterEachStep?.Invoke();
        scheduler.RegisterSerial("cyberland.engine/ui-command-drain", new UiCommandDrainSystem(host));
        afterEachStep?.Invoke();
    }

    /// <summary>
    /// Registers stock early engine systems that should run before gameplay mods append their own systems.
    /// </summary>
    /// <param name="context">Mod load context used to register scheduler entries.</param>
    /// <param name="progressPhaseKey">
    /// When non-null and non-whitespace, each registration step reports monotonic progress via
    /// <see cref="ModLoadContext.ReportLoadProgress"/> under this phase key (host startup UI).
    /// </param>
    public static void RegisterBeforeGameplayMods(ModLoadContext context, string? progressPhaseKey = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scheduler = context.Scheduler;
        var completed = 0;
        void ReportStep()
        {
            if (string.IsNullOrWhiteSpace(progressPhaseKey))
                return;
            completed++;
            context.ReportLoadProgress(progressPhaseKey!, completed / 3f);
        }

        scheduler.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
        ReportStep();
        scheduler.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
        ReportStep();
        scheduler.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem());
        ReportStep();
    }

    /// <summary>
    /// Registers stock late engine systems that submit camera/layout/render/UI state after gameplay updates.
    /// </summary>
    /// <param name="context">Mod load context used to register scheduler entries.</param>
    /// <param name="progressPhaseKey">
    /// When non-null and non-whitespace, each registration step reports monotonic progress via
    /// <see cref="ModLoadContext.ReportLoadProgress"/> under this phase key (host startup UI).
    /// </param>
    public static void RegisterAfterGameplayMods(ModLoadContext context, string? progressPhaseKey = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var host = context.Host;
        var completed = 0;
        const float total = 19f;
        void ReportStep()
        {
            if (string.IsNullOrWhiteSpace(progressPhaseKey))
                return;
            completed++;
            context.ReportLoadProgress(progressPhaseKey!, completed / total);
        }

        RegisterStockLateSystems(context.Scheduler, host, ReportStep);
    }
}
