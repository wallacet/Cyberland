using Cyberland.Demo.FontTest.Components;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.FontTest;

/// <summary>Registers <c>cyberland.demo.fonttest/*</c> types for <c>Scenes/demo_fonttest.json</c>.</summary>
public static class SceneComponentDeserializers
{
    public static void Register(ISceneRuntime scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        scenes.RegisterComponentDeserializer("cyberland.demo.fonttest/ui-root-tag", static (in SceneComponentDeserializeContext ctx) =>
            _ = ctx.World.GetOrAdd<FontTestUiRootTag>(ctx.EntityId));
    }
}
