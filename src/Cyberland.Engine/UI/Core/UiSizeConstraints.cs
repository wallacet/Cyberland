using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Parent-supplied limits for <see cref="UiElement.Measure"/> (+Y down UI space).
/// </summary>
public readonly struct UiSizeConstraints
{
    /// <summary>
    /// Creates constraints; use <see cref="float.PositiveInfinity"/> for unbounded maxima (common for measure passes).
    /// </summary>
    public UiSizeConstraints(float minWidth, float maxWidth, float minHeight, float maxHeight)
    {
        MinWidth = MathF.Max(0f, minWidth);
        MaxWidth = MathF.Max(MinWidth, maxWidth);
        MinHeight = MathF.Max(0f, minHeight);
        MaxHeight = MathF.Max(MinHeight, maxHeight);
    }

    /// <summary>Minimum content width.</summary>
    public float MinWidth { get; init; }

    /// <summary>Maximum content width.</summary>
    public float MaxWidth { get; init; }

    /// <summary>Minimum content height.</summary>
    public float MinHeight { get; init; }

    /// <summary>Maximum content height.</summary>
    public float MaxHeight { get; init; }

    /// <summary>Unbounded maxima (typical root measure).</summary>
    public static UiSizeConstraints Loose(float maxWidth, float maxHeight) =>
        new(0f, maxWidth, 0f, maxHeight);

    /// <summary>Clamps a desired size into [min,max] for each axis.</summary>
    public Vector2D<float> ClampSize(Vector2D<float> desired)
    {
        var w = MathF.Min(MathF.Max(desired.X, MinWidth), MaxWidth);
        var h = MathF.Min(MathF.Max(desired.Y, MinHeight), MaxHeight);
        return new Vector2D<float>(w, h);
    }
}
