using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.MouseChase;

/// <summary>Registers <c>cyberland.demo.mousechase/*</c> types for <c>Scenes/demo_mousechase.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/game-state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<GameState>(ctx.EntityId) = new GameState
            {
                Phase = RoundPhase.Tutorial,
                TutorialStep = 0,
                TimerSeconds = 70f,
                Health = 100f,
                Score = 0,
                TargetScore = 140
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/control-state", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ControlState>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/player-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<PlayerTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/collectible-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<CollectibleTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/enter-zone-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<EnterZoneTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/stay-zone-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<StayZoneTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/exit-zone-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<ExitZoneTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/gate-zone-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<GateZoneTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.mousechase/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<MouseChaseHudRootTag>(ctx.EntityId));
    }
}
