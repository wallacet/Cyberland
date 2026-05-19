using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Serialization;

namespace Cyberland.Demo.Rts;

/// <summary>Registers <c>cyberland.demo.rts/*</c> types for <c>Scenes/demo_rts.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/camera-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RtsCameraTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/camera-zoom-state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<RtsCameraZoomState>(ctx.EntityId) = new RtsCameraZoomState
            {
                TargetViewportWidth = Mod.ViewportWidth,
                TargetViewportHeight = Mod.ViewportHeight
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/unit-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RtsUnitTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/session", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RtsSessionState>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/hud-fps-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RtsHudFpsTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<RtsBackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/selection-bar-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<RtsSelectionBarTag>(ctx.EntityId) = new RtsSelectionBarTag
            {
                Index = (byte)RuntimeJsonReaders.ReadInt(ctx.Data, "index", 0)
            };
        });
    }
}
