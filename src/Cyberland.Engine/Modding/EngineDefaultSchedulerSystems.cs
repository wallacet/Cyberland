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

        var completed = 0;
        void ReportStep()
        {
            if (string.IsNullOrWhiteSpace(progressPhaseKey))
                return;
            completed++;
            context.ReportLoadProgress(progressPhaseKey!, completed / 3f);
        }

        context.RegisterParallel("cyberland.engine/transform2d", new TransformHierarchySystem());
        ReportStep();
        context.RegisterParallel("cyberland.engine/sprite-animation", new SpriteAnimationSystem());
        ReportStep();
        context.RegisterParallel("cyberland.engine/particle-sim", new ParticleSimulationSystem());
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

        context.RegisterParallel("cyberland.engine/camera-follow", new CameraFollowSystem());
        ReportStep();
        context.RegisterParallel("cyberland.engine/trigger", new TriggerSystem());
        ReportStep();
        // Publish camera runtime state before viewport anchors so gameplay/layout reads deterministic ECS-owned
        // camera data instead of renderer queue snapshots.
        context.RegisterSerial("cyberland.engine/camera-submit", new CameraSubmitSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/camera-runtime-state", new CameraRuntimeStateSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/viewport-layout", new ViewportAnchorSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/lighting-ambient", new AmbientLightSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/lighting-directional", new DirectionalLightSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/lighting-spot", new SpotLightSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/lighting-point", new PointLightSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/global-post-process", new GlobalPostProcessSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/post-process-volumes", new PostProcessVolumeSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/tilemap-render", new TilemapRenderSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/sprite-localized-assets", new SpriteLocalizedAssetSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/sprite-render", new SpriteRenderSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/particle-render", new ParticleRenderSystem(host));
        ReportStep();
        context.RegisterParallel("cyberland.engine/text-staging", new TextStagingSystem());
        ReportStep();
        // Bitmap text build is folded into TextRenderSystem; it parallelizes per chunk/range and submits thread-safe requests.
        context.RegisterParallel("cyberland.engine/text-render", new TextRenderSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/ui-document-frame", new UiDocumentFrameSystem(host));
        ReportStep();
        context.RegisterSerial("cyberland.engine/ui-command-drain", new UiCommandDrainSystem(host));
        ReportStep();
    }
}
