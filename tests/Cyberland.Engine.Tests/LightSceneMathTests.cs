using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
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

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void DirectionFromWorldRotation_returns_fallback_for_non_finite(float radians)
    {
        var dir = LightSceneMath.DirectionFromWorldRotation(radians);
        Assert.Equal(1f, dir.X);
        Assert.Equal(0f, dir.Y);
    }

    [Fact]
    public void MaxAbsScale_returns_fallback_for_non_finite()
    {
        var s = LightSceneMath.MaxAbsScale(new Vector2D<float>(float.NaN, 2f));
        Assert.Equal(1f, s);
    }

    [Fact]
    public void ResolveLightPositionWorldForSubmit_world_rooted_returns_translation()
    {
        // World-rooted light (no viewport sprite root): position passes through unchanged.
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;
        ref var spr = ref world.GetOrAdd<Sprite>(root);
        spr.Space = CoordinateSpace.WorldSpace;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        var cam = new CameraRuntimeState(
            new Vector2D<int>(1280, 720),
            new Vector2D<float>(640f, 360f),
            0f,
            default,
            0,
            Valid: true);
        var pos = new Vector2D<float>(123f, 456f);

        var result = LightSceneMath.ResolveLightPositionWorldForSubmit(world, child, pos, in cam);
        Assert.Equal(pos.X, result.X, 3);
        Assert.Equal(pos.Y, result.Y, 3);
    }

    [Fact]
    public void ResolveLightPositionWorldForSubmit_no_sprite_root_returns_translation()
    {
        // Entity hierarchy without any Sprite component: position passes through.
        var world = new World();
        var root = world.CreateEntity();
        world.GetOrAdd<Transform>(root) = Transform.Identity;

        var child = world.CreateEntity();
        var tChild = Transform.Identity;
        tChild.Parent = root;
        world.GetOrAdd<Transform>(child) = tChild;

        var cam = new CameraRuntimeState(
            new Vector2D<int>(1280, 720),
            new Vector2D<float>(640f, 360f),
            0f,
            default,
            0,
            Valid: true);
        var pos = new Vector2D<float>(77f, 88f);

        var result = LightSceneMath.ResolveLightPositionWorldForSubmit(world, child, pos, in cam);
        Assert.Equal(pos.X, result.X, 3);
        Assert.Equal(pos.Y, result.Y, 3);
    }
}
