using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Simple translation component in **world space**: origin bottom-left, +X right, +Y up (usually pixels). See <see cref="WorldScreenSpace"/> for conversion to screen space.
/// </summary>
public struct Position
{
    /// <summary>Horizontal position in world units.</summary>
    public float X;
    /// <summary>Vertical position in world units (up is positive).</summary>
    public float Y;

    /// <summary>Packs <see cref="X"/> and <see cref="Y"/> into a vector.</summary>
    public Vector2D<float> AsVector() => new(X, Y);

    /// <summary>Builds a <see cref="Position"/> from a vector (same coordinate convention).</summary>
    public static Position FromVector(Vector2D<float> v)
    {
        Position p;
        p.X = v.X;
        p.Y = v.Y;
        return p;
    }
}
