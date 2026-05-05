namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Four-sided inset (margin/padding) in pixels.
/// </summary>
public readonly struct UiThickness : IEquatable<UiThickness>
{
    /// <summary>Left inset.</summary>
    public float Left { get; init; }

    /// <summary>Top inset.</summary>
    public float Top { get; init; }

    /// <summary>Right inset.</summary>
    public float Right { get; init; }

    /// <summary>Bottom inset.</summary>
    public float Bottom { get; init; }

    /// <summary>Uniform inset on all sides.</summary>
    public UiThickness(float uniform)
    {
        Left = Top = Right = Bottom = uniform;
    }

    /// <summary>Symmetric horizontal / vertical pairs.</summary>
    public UiThickness(float horizontal, float vertical)
    {
        Left = Right = horizontal;
        Top = Bottom = vertical;
    }

    /// <summary>Full control over each side.</summary>
    public UiThickness(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Horizontal sum (Left + Right).</summary>
    public float Horizontal => Left + Right;

    /// <summary>Vertical sum (Top + Bottom).</summary>
    public float Vertical => Top + Bottom;

    /// <inheritdoc />
    public bool Equals(UiThickness other) =>
        Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) &&
        Bottom.Equals(other.Bottom);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiThickness t && Equals(t);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    /// <summary>Equality.</summary>
    public static bool operator ==(UiThickness a, UiThickness b) => a.Equals(b);

    /// <summary>Inequality.</summary>
    public static bool operator !=(UiThickness a, UiThickness b) => !a.Equals(b);
}
