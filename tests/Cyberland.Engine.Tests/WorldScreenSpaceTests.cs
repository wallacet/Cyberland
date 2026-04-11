using Cyberland.Engine;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class WorldScreenSpaceTests
{
    [Fact]
    public void WorldCenterToScreenPixel_flips_y_using_framebuffer_height()
    {
        var fb = new Vector2D<int>(800, 600);
        var world = new Vector2D<float>(100f, 400f);
        var screen = WorldScreenSpace.WorldCenterToScreenPixel(world, fb);
        Assert.Equal(100f, screen.X);
        Assert.Equal(200f, screen.Y);
    }

    [Fact]
    public void WorldVelocityToScreenVelocity_negates_y()
    {
        var v = new Vector2D<float>(3f, -4f);
        var s = WorldScreenSpace.WorldVelocityToScreenVelocity(v);
        Assert.Equal(3f, s.X);
        Assert.Equal(4f, s.Y);
    }

    [Fact]
    public void ScreenPixelToWorldCenter_inverts_y_to_world_space()
    {
        var fb = new Vector2D<int>(1280, 720);
        var screen = new Vector2D<float>(640f, 100f);
        var w = WorldScreenSpace.ScreenPixelToWorldCenter(screen, fb);
        Assert.Equal(640f, w.X);
        Assert.Equal(620f, w.Y);
    }
}
