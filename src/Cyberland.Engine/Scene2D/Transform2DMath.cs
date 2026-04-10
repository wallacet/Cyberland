using System.Numerics;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>Pure 2D TRS math for hierarchy and sprite submission (unit-tested).</summary>
public static class Transform2DMath
{
    /// <summary>Local matrix: applies scale, then rotation, then translation (column vectors).</summary>
    public static Matrix3x2 LocalMatrix(Vector2D<float> localPos, float localRad, Vector2D<float> localScale)
    {
        var s = Matrix3x2.CreateScale(localScale.X, localScale.Y);
        var r = Matrix3x2.CreateRotation(localRad);
        var tr = Matrix3x2.CreateTranslation(localPos.X, localPos.Y);
        return Matrix3x2.Multiply(tr, Matrix3x2.Multiply(r, s));
    }

    public static Matrix3x2 LocalMatrix(in Transform t) =>
        LocalMatrix(t.LocalPosition, t.LocalRotationRadians, t.LocalScale);

    /// <summary>World = parent * local (child expressed in parent space).</summary>
    public static Matrix3x2 Compose(in Matrix3x2 parentWorld, in Matrix3x2 local) =>
        Matrix3x2.Multiply(parentWorld, local);

    public static Matrix3x2 MatrixFromPositionRotationScale(Vector2D<float> pos, float radians, Vector2D<float> scale)
    {
        var s = Matrix3x2.CreateScale(scale.X, scale.Y);
        var r = Matrix3x2.CreateRotation(radians);
        var tr = Matrix3x2.CreateTranslation(pos.X, pos.Y);
        return Matrix3x2.Multiply(tr, Matrix3x2.Multiply(r, s));
    }

    public static void DecomposeToPRS(in Matrix3x2 m, out Vector2D<float> pos, out float radians, out Vector2D<float> scale)
    {
        pos = new Vector2D<float>(m.Translation.X, m.Translation.Y);
        var sx = MathF.Sqrt(m.M11 * m.M11 + m.M21 * m.M21);
        var sy = MathF.Sqrt(m.M12 * m.M12 + m.M22 * m.M22);
        if (sx < 1e-8f || sy < 1e-8f)
        {
            radians = 0f;
            scale = new Vector2D<float>(0f, 0f);
            return;
        }

        radians = MathF.Atan2(m.M21, m.M11);
        scale = new Vector2D<float>(sx, sy);
    }
}
