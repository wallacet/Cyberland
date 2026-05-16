using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.IdleGold;

/// <summary>Registers <c>cyberland.demo.idlegold/*</c> types for <c>Scenes/demo_idlegold.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.idlegold/session-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<SessionTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.idlegold/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<IdleGoldHudRootTag>(ctx.EntityId));
    }
}
