using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo;

/// <summary>
/// Scene JSON component types for the HDR tutorial (<c>Scenes/hdr.json</c>).
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

        scenes.RegisterComponentDeserializer("cyberland.demo/shadow-floor-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<ShadowFloorTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudRootTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/warm-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<WarmPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/player-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<PlayerPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/bloom-volume-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<BloomVolumeTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/velocity", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Velocity>(ctx.EntityId));
    }
}
