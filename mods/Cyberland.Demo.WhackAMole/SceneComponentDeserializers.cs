using Cyberland.Demo.WhackAMole.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.WhackAMole;

/// <summary>Registers <c>cyberland.demo.whackamole/*</c> types for <c>Scenes/demo_whackamole.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<WhackAMoleState>(ctx.EntityId) = new WhackAMoleState
            {
                Phase = WhackAMolePhase.Ready,
                Score = 0,
                TimeRemainingSeconds = 60f,
                TimerStarted = false
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/target-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<WhackAMoleTargetTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/score-text-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<WhackAMoleScoreTextTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/timer-text-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<WhackAMoleTimerTextTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/overlay-text-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<WhackAMoleOverlayTextTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.whackamole/target-fill-light-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<TargetFillLightTag>(ctx.EntityId));
    }
}
