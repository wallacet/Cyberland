using Cyberland.Demo.Audio.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Audio;

/// <summary>Mod tag deserializers for the audio demo scene.</summary>
public static class SceneComponentDeserializers
{
    /// <summary>Registers demo tags.</summary>
    public static void Register(ISceneRuntime scenes)
    {
        scenes.RegisterComponentDeserializer("cyberland.demo.audio/player-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<PlayerTag>(ctx.EntityId) = default;
        });
        scenes.RegisterComponentDeserializer("cyberland.demo.audio/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<HudRootTag>(ctx.EntityId) = default;
        });
        scenes.RegisterComponentDeserializer("cyberland.demo.audio/street-env-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<StreetEnvTag>(ctx.EntityId) = default;
        });
        scenes.RegisterComponentDeserializer("cyberland.demo.audio/club-env-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<ClubEnvTag>(ctx.EntityId) = default;
        });
    }
}
