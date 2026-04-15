using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Bitmap label drawn by <see cref="Systems.TextStagingSystem"/> then <see cref="Systems.TextRenderSystem"/>; pair with <see cref="Position"/> for baseline-left.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="TextCoordinateSpace.WorldBaseline"/> for diegetic labels in the playfield; use <see cref="TextCoordinateSpace.ScreenPixels"/> for HUD chrome.
/// Screen-space rows often pair with <see cref="ViewportAnchor2D"/> so <see cref="Position"/> tracks resize.
/// </para>
/// <para>
/// Recommended <see cref="SortKey"/> bands: lower values for world text, higher (e.g. 400+) for UI so HUD stacks above gameplay.
/// </para>
/// </remarks>
public struct BitmapText
{
    /// <summary>When false, the text pass skips this row.</summary>
    public bool Visible;

    /// <summary>When true, <see cref="Content"/> is resolved through the active <c>LocalizationManager</c>.</summary>
    public bool IsLocalizationKey;

    /// <summary>Literal copy or localization key, depending on <see cref="IsLocalizationKey"/>.</summary>
    public string Content;

    /// <summary>Font, size, color, and decorations.</summary>
    public TextStyle Style;

    /// <summary>Tie-break for draw order among UI text (passed to <see cref="TextRenderer"/>).</summary>
    public float SortKey;

    /// <summary>Whether <see cref="Position"/> is world (+Y up) or screen pixels (+Y down).</summary>
    public TextCoordinateSpace CoordinateSpace;
}
