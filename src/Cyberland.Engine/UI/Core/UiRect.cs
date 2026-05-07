using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Axis-aligned rectangle in UI space — origin top-left, +Y down, units in pixels (viewport-local until root transform).
/// </summary>
public readonly struct UiRect : IEquatable<UiRect>
{
    /// <summary>Left edge (pixels).</summary>
    public float X { get; init; }

    /// <summary>Top edge (pixels).</summary>
    public float Y { get; init; }

    /// <summary>Width (non-negative).</summary>
    public float Width { get; init; }

    /// <summary>Height (non-negative).</summary>
    public float Height { get; init; }

    /// <summary>Creates a rectangle with non-negative width and height (negative sizes clamp to 0).</summary>
    public UiRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = MathF.Max(0f, width);
        Height = MathF.Max(0f, height);
    }

    /// <summary>Right edge (X + Width).</summary>
    public float Right => X + Width;

    /// <summary>Bottom edge (Y + Height).</summary>
    public float Bottom => Y + Height;

    /// <summary>Center point.</summary>
    public Vector2D<float> Center => new(X + Width * 0.5f, Y + Height * 0.5f);

    /// <summary>Returns true if <paramref name="point"/> lies inside or on the border.</summary>
    public bool Contains(Vector2D<float> point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>Shrinks the rectangle by <paramref name="insets"/> (negative sizes clamp to 0).</summary>
    public UiRect Deflate(UiThickness insets)
    {
        var w = Width - insets.Horizontal;
        var h = Height - insets.Vertical;
        return new UiRect(X + insets.Left, Y + insets.Top, MathF.Max(0f, w), MathF.Max(0f, h));
    }

    /// <summary>
    /// Intersects this rect with <paramref name="clip"/> by clamping inclusive min/max edges.
    /// </summary>
    public UiRect Intersect(in UiRect clip)
    {
        var x0 = MathF.Max(X, clip.X);
        var y0 = MathF.Max(Y, clip.Y);
        var x1 = MathF.Min(Right, clip.Right);
        var y1 = MathF.Min(Bottom, clip.Bottom);
        return new UiRect(x0, y0, MathF.Max(0f, x1 - x0), MathF.Max(0f, y1 - y0));
    }

    /// <inheritdoc />
    public bool Equals(UiRect other) =>
        X.Equals(other.X) && Y.Equals(other.Y) && Width.Equals(other.Width) && Height.Equals(other.Height);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiRect r && Equals(r);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    /// <summary>Equality.</summary>
    public static bool operator ==(UiRect a, UiRect b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(UiRect a, UiRect b) => !a.Equals(b);
}
