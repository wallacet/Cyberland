using Cyberland.Engine;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class WorldViewportSpaceTests
{
    [Fact]
    public void WorldCenterToViewportPixel_flips_y_using_framebuffer_height()
    {
        var fb = new Vector2D<int>(800, 600);
        var world = new Vector2D<float>(100f, 400f);
        var viewport = WorldViewportSpace.WorldCenterToViewportPixel(world, fb);
        Assert.Equal(100f, viewport.X);
        Assert.Equal(200f, viewport.Y);
    }

    [Fact]
    public void WorldVelocityToViewportVelocity_negates_y()
    {
        var v = new Vector2D<float>(3f, -4f);
        var s = WorldViewportSpace.WorldVelocityToViewportVelocity(v);
        Assert.Equal(3f, s.X);
        Assert.Equal(4f, s.Y);
    }

    [Fact]
    public void ViewportPixelToWorldCenter_inverts_y_to_world_space()
    {
        var fb = new Vector2D<int>(1280, 720);
        var viewport = new Vector2D<float>(640f, 100f);
        var w = WorldViewportSpace.ViewportPixelToWorldCenter(viewport, fb);
        Assert.Equal(640f, w.X);
        Assert.Equal(620f, w.Y);
    }
}
