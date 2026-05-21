using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Serialization;

namespace Cyberland.Demo.Rts;

/// <summary>Registers <c>cyberland.demo.rts/*</c> types for <c>Scenes/rts.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/camera-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<CameraTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/camera-zoom-state", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<CameraZoomState>(ctx.EntityId) = new CameraZoomState
            {
                TargetViewportWidth = Mod.ViewportWidth,
                TargetViewportHeight = Mod.ViewportHeight
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/unit-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<UnitTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/unit-state", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<UnitState>(ctx.EntityId) = new UnitState
            {
                Selected = false,
                HasMoveOrder = false,
                MoveTargetWorld = default
            });

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/session", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<SessionState>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudRootTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<BackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.rts/selection-bar-tag", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SelectionBarTag>(ctx.EntityId) = new SelectionBarTag
            {
                Index = (byte)RuntimeJsonReaders.ReadInt(ctx.Data, "index", 0)
            };
        });
    }
}
