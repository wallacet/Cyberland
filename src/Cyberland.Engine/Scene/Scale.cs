using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>Per-axis scale applied before rotation when building a transform (1 = identity).</summary>
public struct Scale
{
    /// <summary>Horizontal scale factor.</summary>
    public float X;
    /// <summary>Vertical scale factor.</summary>
    public float Y;

    /// <summary>Identity scale (1,1).</summary>
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

    /// <summary>Interprets scale as an (x, y) pair (non-negative expected for sensible rendering).</summary>
    public Vector2D<float> AsVector() => new(X, Y);
}
