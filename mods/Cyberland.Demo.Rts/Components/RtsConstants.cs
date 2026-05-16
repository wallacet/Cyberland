using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>World layout and tuning for the minimal RTS demo scene.</summary>
public static class RtsConstants
{
    public const float PlaySize = 3000f;
    public const float StopEpsilon = 6f;
    public const float MoveSpeed = 240f;
    public const float PanSpeedKeyboard = 520f;
    public const float PanSpeedEdge = 480f;
    public const int EdgeScrollMarginPx = 24;
    public const float SelectionPadding = 3f;
    public static readonly Vector2D<float> UnitHalfExtents = new(24f, 24f);

    /// <summary>Virtual viewport width bounds for mouse-wheel zoom (height follows 16:9).</summary>
    public const float ZoomViewportMinWidth = 560f;

    public const float ZoomViewportMaxWidth = 2200f;

    /// <summary>Exponential smoothing rate toward <see cref="RtsCameraZoomState"/> (higher = snappier).</summary>
    public const float ZoomSmoothingPerSecond = 12f;
}
