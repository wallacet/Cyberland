using System.Numerics;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Pure 2D TRS math for <see cref="Transform"/> matrix storage, hierarchy composition, and sprite submission.
/// </summary>
/// <remarks>
/// <para>
/// The canonical 2D transform type is <see cref="Matrix3x2"/>, a compact representation of a 3×3 homogeneous affine
/// matrix (implicit third column <c>(0, 0, 1)</c>). All helpers follow .NET's row-vector convention: points multiply
/// on the left (<see cref="Vector2.Transform(Vector2,Matrix3x2)"/>), and matrix chains compose left-to-right as
/// <c>v * A * B</c> — i.e. the matrix closest to the vector is applied first.
/// </para>
/// <para>
/// <b>PRS build order:</b> scale first, then rotation, then translation (<c>v * S * R * T</c>). This is the standard
/// TRS convention where authoring PRS triples (position, rotation, scale) reconstruct the matrix unambiguously.
/// </para>
/// <para>
/// <b>Composition order:</b> child world = <c>local * parent</c>. A child-space point is first transformed by the
/// child's local matrix into parent space, then by the parent's world matrix into world space.
/// </para>
/// </remarks>
public static class TransformMath
{
    /// <summary>
    /// Builds a 2D affine matrix from position, rotation, and scale using the TRS convention (scale → rotation →
    /// translation when applied to a row vector).
    /// </summary>
    /// <param name="pos">Translation in the target space (parent for local, world for resolved).</param>
    /// <param name="radians">CCW rotation in radians.</param>
    /// <param name="scale">Non-uniform scale applied before rotation.</param>
    public static Matrix3x2 MatrixFromPositionRotationScale(Vector2D<float> pos, float radians, Vector2D<float> scale)
    {
        var s = Matrix3x2.CreateScale(scale.X, scale.Y);
        var r = Matrix3x2.CreateRotation(radians);
        var tr = Matrix3x2.CreateTranslation(pos.X, pos.Y);
        // Row-vector convention: v * S * R * T applies S, then R, then T. Matrix3x2.Multiply(a, b) returns a * b.
        return Matrix3x2.Multiply(Matrix3x2.Multiply(s, r), tr);
    }

    /// <summary>Composes a child's local transform with its parent's world transform: <c>childWorld = local * parent</c>.</summary>
    /// <remarks>
    /// Row-vector convention: a point <c>v * childWorld</c> is first transformed by <paramref name="local"/> (into
    /// parent space), then by <paramref name="parentWorld"/> (into world space).
    /// </remarks>
    public static Matrix3x2 Compose(in Matrix3x2 parentWorld, in Matrix3x2 local) =>
        Matrix3x2.Multiply(local, parentWorld);

    /// <summary>
    /// Approximate decomposition of a 2D affine matrix built via <see cref="MatrixFromPositionRotationScale"/> into
    /// position, rotation, and positive scale.
    /// </summary>
    /// <remarks>
    /// Matches the inverse of the TRS build convention: translation comes directly from row 2 of the matrix, scale
    /// from the length of each of the first two rows, and rotation from <c>atan2(M12, M11)</c>. When either
    /// reconstructed scale is effectively zero (matrix collapses to a point), rotation is reported as 0 and scale as
    /// <c>(0, 0)</c> to avoid returning NaN.
    /// </remarks>
    public static void DecomposeToPRS(in Matrix3x2 m, out Vector2D<float> pos, out float radians, out Vector2D<float> scale)
    {
        pos = new Vector2D<float>(m.M31, m.M32);
        // Row 0 of the 3x3 is (sx*cos, sx*sin, 0); its length recovers sx. Row 1 is (-sy*sin, sy*cos, 0); length = sy.
        var sx = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
        var sy = MathF.Sqrt(m.M21 * m.M21 + m.M22 * m.M22);
        if (sx < 1e-8f || sy < 1e-8f)
        {
            radians = 0f;
            scale = new Vector2D<float>(0f, 0f);
            return;
        }

        radians = MathF.Atan2(m.M12, m.M11);
        scale = new Vector2D<float>(sx, sy);
    }
}
