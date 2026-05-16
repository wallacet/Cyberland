using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class LightSceneMathTests
{
    [Fact]
    public void RootTransformUsesViewportSpriteSpace_matches_try_get_when_root_has_viewport_sprite()
    {
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;
        ref var spr = ref world.GetOrAdd<Sprite>(root);
        spr.Space = CoordinateSpace.ViewportSpace;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        Assert.True(LightSceneMath.RootTransformUsesViewportSpriteSpace(world, child));
    }

    [Fact]
    public void RootTransformUsesViewportSpriteSpace_is_true_for_presentation_viewport_sprite_root()
    {
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;
        ref var spr = ref world.GetOrAdd<Sprite>(root);
        spr.Space = CoordinateSpace.PresentationViewportSpace;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        Assert.True(LightSceneMath.RootTransformUsesViewportSpriteSpace(world, child));
    }

    [Fact]
    public void RootTransformUsesViewportSpriteSpace_is_false_when_root_sprite_is_world_space()
    {
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;
        ref var spr = ref world.GetOrAdd<Sprite>(root);
        spr.Space = CoordinateSpace.WorldSpace;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        Assert.False(LightSceneMath.RootTransformUsesViewportSpriteSpace(world, child));
    }

    [Fact]
    public void RootTransformUsesViewportSpriteSpace_is_false_when_chain_has_no_sprite()
    {
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        Assert.False(LightSceneMath.RootTransformUsesViewportSpriteSpace(world, child));
    }
}
