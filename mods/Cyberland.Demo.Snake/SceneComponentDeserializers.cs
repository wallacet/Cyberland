using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.Snake;

/// <summary>Registers <c>cyberland.demo.snake/*</c> types for <c>Scenes/demo_snake.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/control", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Control>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/session", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Session>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/visual-bundle", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<VisualBundle>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/tilemap", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<Tilemap>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/head-follow-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HeadFollowPointLightTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.snake/food-follow-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<FoodFollowPointLightTag>(ctx.EntityId));
    }
}
