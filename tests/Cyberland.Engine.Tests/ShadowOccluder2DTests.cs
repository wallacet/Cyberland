using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class ShadowOccluder2DTests
{
    [Fact]
    public void Constructor_assigns_world_space_fields()
    {
        var occluder = new ShadowOccluder2D(
            centerWorld: new Vector2D<float>(553f, 373f),
            halfExtentsWorld: new Vector2D<float>(28f, 110f),
            rotationRadians: 0.5f);
        Assert.Equal(553f, occluder.CenterWorld.X);
        Assert.Equal(373f, occluder.CenterWorld.Y);
        Assert.Equal(28f, occluder.HalfExtentsWorld.X);
        Assert.Equal(110f, occluder.HalfExtentsWorld.Y);
        Assert.Equal(0.5f, occluder.RotationRadians);
    }

    [Fact]
    public void ContainsPointWorld_true_for_center()
    {
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(100f, 100f),
            new Vector2D<float>(10f, 20f),
            0f);
        Assert.True(occluder.ContainsPointWorld(new Vector2D<float>(100f, 100f)));
    }

    [Fact]
    public void ContainsPointWorld_false_outside_axis_aligned_box()
    {
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(100f, 100f),
            new Vector2D<float>(10f, 20f),
            0f);
        Assert.False(occluder.ContainsPointWorld(new Vector2D<float>(115f, 100f)));
        Assert.False(occluder.ContainsPointWorld(new Vector2D<float>(100f, 125f)));
    }

    [Fact]
    public void ContainsPointWorld_respects_rotation()
    {
        // 45-degree rotated thin box: a point that is outside the axis-aligned box's slab is INSIDE the rotated one.
        var occluder = new ShadowOccluder2D(
            new Vector2D<float>(0f, 0f),
            new Vector2D<float>(20f, 5f),
            MathF.PI / 4f);
        Assert.True(occluder.ContainsPointWorld(new Vector2D<float>(10f, 10f)));
        // Far away along the (now off-axis) thin dimension stays outside.
        Assert.False(occluder.ContainsPointWorld(new Vector2D<float>(0f, 30f)));
    }
}
