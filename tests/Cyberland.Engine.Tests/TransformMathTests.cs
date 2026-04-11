using System.Numerics;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class TransformMathTests
{
    [Fact]
    public void LocalMatrix_translation_without_rotation()
    {
        var m = TransformMath.LocalMatrix(new Vector2D<float>(10f, 20f), 0f, new Vector2D<float>(1f, 1f));
        Assert.Equal(10f, m.M31, 4);
        Assert.Equal(20f, m.M32, 4);
    }

    [Fact]
    public void Compose_multiplies_parent_and_local()
    {
        var parent = Matrix3x2.CreateTranslation(5f, 0f);
        var local = Matrix3x2.CreateTranslation(0f, 7f);
        var w = TransformMath.Compose(parent, local);
        Assert.Equal(5f, w.M31, 3);
        Assert.Equal(7f, w.M32, 3);
    }

    [Fact]
    public void DecomposeToPRS_round_trips_translation_only()
    {
        var m = TransformMath.MatrixFromPositionRotationScale(new Vector2D<float>(3f, 4f), 0f, new Vector2D<float>(1f, 1f));
        TransformMath.DecomposeToPRS(m, out var pos, out var rad, out var scale);
        Assert.Equal(3f, pos.X, 3);
        Assert.Equal(4f, pos.Y, 3);
        Assert.Equal(0f, rad, 3);
        Assert.Equal(1f, scale.X, 3);
        Assert.Equal(1f, scale.Y, 3);
    }

    [Fact]
    public void DecomposeToPRS_handles_near_zero_scale()
    {
        var m = new Matrix3x2(1e-9f, 0f, 0f, 1e-9f, 1f, 2f);
        TransformMath.DecomposeToPRS(m, out _, out var rad, out var scale);
        Assert.Equal(0f, rad);
        Assert.Equal(0f, scale.X);
        Assert.Equal(0f, scale.Y);
    }
}
