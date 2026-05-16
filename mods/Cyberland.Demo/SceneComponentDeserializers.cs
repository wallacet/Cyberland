using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo;

/// <summary>
/// Scene JSON component types for the HDR tutorial (<c>Scenes/demo_hdr.json</c>).
/// Stock <c>cyberland.engine/*</c> types register automatically on <see cref="ISceneRuntime"/>.
/// </summary>
public static class SceneComponentDeserializers
{
    /// <summary>Registers <c>cyberland.demo/*</c> marker components used by HDR systems.</summary>
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo/player-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<PlayerTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<BackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/neon-strip-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<NeonStripTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-title-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudTitleTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-hint-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudHintTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-fps-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudFpsTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-warm-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrWarmPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-player-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrPlayerPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-bloom-volume-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrBloomVolumeTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/velocity", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Velocity>(ctx.EntityId));
    }
}
