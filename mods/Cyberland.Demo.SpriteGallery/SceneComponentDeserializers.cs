using Cyberland.Demo.SpriteGallery.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.SpriteGallery;

/// <summary>Registers <c>cyberland.demo.spritegallery/*</c> scene component types.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.spritegallery/gallery-state", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<GalleryState>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo.spritegallery/hud-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<HudRootTag>(ctx.EntityId));
    }
}
