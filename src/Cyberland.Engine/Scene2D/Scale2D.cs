using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>Non-uniform scale in world space.</summary>
public struct Scale
{
    public float X;
    public float Y;

    public static Scale One
    {
        get
        {
            Scale s;
            s.X = 1f;
            s.Y = 1f;
            return s;
        }
    }

    public Vector2D<float> AsVector() => new(X, Y);
}
