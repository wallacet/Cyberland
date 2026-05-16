using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeScenes.Serialization;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Registers <c>cyberland.demo.brick/*</c> types for <c>Scenes/demo_brickbreaker.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/session-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<SessionTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/control-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ControlTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/ball-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BallTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/title-ui-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<TitleUiTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/game-over-panel-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<GameOverPanelTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/game-over-bar-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<GameOverBarTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-title-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudTitleTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-hint-title-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudHintTitleTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-game-over-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudGameOverTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-hint-end-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudHintEndTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-playing-score-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudPlayingScoreTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-score-num-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudScoreNumTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/hud-fps-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudFpsTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/ambient-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<AmbientLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/directional-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<DirectionalLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/arena-spot-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ArenaSpotLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/paddle-point-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<PaddlePointLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/ball-point-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BallPointLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/game-state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<GameState>(ctx.EntityId) = new GameState
            {
                Phase = Phase.Title,
                Lives = Constants.StartingLives,
                BallDocked = true
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/control", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Control>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/paddle", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Paddle>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/paddle-body", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<PaddleBody>(ctx.EntityId) = new PaddleBody
            {
                HalfWidth = SceneComponentJson.ReadFloat(ctx.Data, "halfWidth", 72f),
                HalfHeight = SceneComponentJson.ReadFloat(ctx.Data, "halfHeight", 10f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/velocity", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Velocity>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/cell", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<Cell>(ctx.EntityId) = new Cell
            {
                X = SceneComponentJson.ReadInt(ctx.Data, "x", 0),
                Y = SceneComponentJson.ReadInt(ctx.Data, "y", 0)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/arena-cell-state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<ArenaCellState>(ctx.EntityId) = new ArenaCellState
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.brick/life-pip-slot", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<LifePipSlot>(ctx.EntityId) = new LifePipSlot
            {
                Index = (byte)SceneComponentJson.ReadInt(ctx.Data, "index", 0)
            };
        });
    }
}
