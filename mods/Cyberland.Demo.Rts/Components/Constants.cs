using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>World layout and tuning for the minimal RTS demo scene.</summary>
public static class Constants
{
    public const float PlaySize = 3000f;
    public const float StopEpsilon = 6f;
    public const float MoveSpeed = 240f;
    public const float PanSpeedKeyboard = 520f;
    public const float PanSpeedEdge = 480f;
    public const int EdgeScrollMarginPx = 24;
    public const float SelectionPadding = 3f;
    public static readonly Vector2D<float> UnitHalfExtents = new(24f, 24f);

    public const int UnitCount = 10;

    /// <summary>Center-to-center spacing for formation grid slots (≥ 2× unit diameter + gap).</summary>
    public const float FormationSpacing = 56f;

    /// <summary>Circle radius for pairwise separation (matches unit half-extent).</summary>
    public const float SeparationRadius = 24f;

    public const int SeparationIterations = 3;

    /// <summary>Left-drag shorter than this (swapchain px) is treated as a click, not a box select.</summary>
    public const float BoxSelectMinDragScreenPx = 8f;

    /// <summary>Spawn grid columns when creating units in <see cref="Mod.WirePlayfieldAfterSpawn"/>.</summary>
    public const int SpawnGridColumns = 5;

    /// <summary>Virtual viewport width bounds for mouse-wheel zoom (height follows 16:9).</summary>
    public const float ZoomViewportMinWidth = 560f;

    public const float ZoomViewportMaxWidth = 2200f;

    /// <summary>Exponential smoothing rate toward <see cref="CameraZoomState"/> (higher = snappier).</summary>
    public const float ZoomSmoothingPerSecond = 12f;

    public const int ControlGroupCount = 10;

    /// <summary>Max seconds between digit presses to count as a control-group double-tap.</summary>
    public const float GroupDoubleTapSeconds = 0.35f;
}
