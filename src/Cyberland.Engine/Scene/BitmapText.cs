using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Bitmap label drawn by <see cref="Systems.TextRenderSystem"/>; pair with <see cref="Position"/> for baseline-left.
/// </summary>
/// <remarks>
/// When <see cref="BaselineWorldSpace"/> is true, <see cref="Position"/> is world space (+Y up). When false, <see cref="Position.X"/> /
/// <see cref="Position.Y"/> are framebuffer pixels (top-left origin, +Y down), matching <see cref="TextRenderer.DrawLiteralScreen"/>.
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

    /// <summary>World vs screen interpretation of <see cref="Position"/> (see summary).</summary>
    public bool BaselineWorldSpace;
}
