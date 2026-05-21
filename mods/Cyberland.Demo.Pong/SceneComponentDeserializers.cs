using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Pong;

/// <summary>Registers <c>cyberland.demo.pong/*</c> scene JSON types for <c>Scenes/pong.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.pong/state", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<State>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.pong/control", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Control>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.pong/ball-accent-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BallAccentPointLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.pong/left-paddle-accent-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<LeftPaddleAccentPointLightTag>(ctx.EntityId));

        RegisterSpriteTags(scenes);
        RegisterHudTags(scenes);
    }

    private static void RegisterSpriteTags(ISceneRuntime scenes)
    {
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/background-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BackgroundSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/title-bar-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<TitleBarSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/hint-bar-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HintBarSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/score-player-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ScorePlayerSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/score-cpu-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ScoreCpuSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/left-paddle-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<LeftPaddleSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/right-paddle-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RightPaddleSpriteTag>(ctx.EntityId));
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/ball-sprite-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BallSpriteTag>(ctx.EntityId));
    }

    private static void RegisterHudTags(ISceneRuntime scenes)
    {
        scenes.RegisterComponentDeserializer("cyberland.demo.pong/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudRootTag>(ctx.EntityId));
    }
}
