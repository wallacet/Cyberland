using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>World-space position in 2D (+Y up, same units as <see cref="Engine.WorldScreenSpace"/>).</summary>
public struct Position
{
    public float X;
    public float Y;

    public Vector2D<float> AsVector() => new(X, Y);

    public static Position FromVector(Vector2D<float> v)
    {
        Position p;
        p.X = v.X;
        p.Y = v.Y;
        return p;
    }
}
