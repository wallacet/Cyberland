namespace Cyberland.Engine.Scene;

/// <summary>
/// Corner / edge placement used by <see cref="ViewportAnchor2D"/> with <see cref="ViewportAnchor2D.OffsetX"/> / <see cref="ViewportAnchor2D.OffsetY"/>.
/// </summary>
/// <remarks>
/// Horizontal: for left presets, <see cref="ViewportAnchor2D.OffsetX"/> is inset from the left edge; for right presets, inset from the right edge.
/// Vertical: for top presets, <see cref="ViewportAnchor2D.OffsetY"/> is inset from the top edge (viewport space) or top in world space; for bottom presets, inset from the bottom edge.
/// </remarks>
public enum ViewportAnchorPreset
{
    /// <summary><see cref="ViewportAnchor2D.OffsetX"/> / <see cref="ViewportAnchor2D.OffsetY"/> from top-left (viewport) or bottom-left (world) as appropriate.</summary>
    TopLeft,

    /// <summary>Top-right corner.</summary>
    TopRight,

    /// <summary>Bottom-left corner.</summary>
    BottomLeft,

    /// <summary>Bottom-right corner.</summary>
    BottomRight,

    /// <summary>Viewport center plus offsets.</summary>
    Center,

    /// <summary>Left edge at <see cref="ViewportAnchor2D.OffsetX"/>; vertically centered.</summary>
    LeftCenter
}
