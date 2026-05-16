using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Bitmap label: <see cref="Systems.TextStagingSystem"/> warns on misconfiguration;
/// <see cref="Systems.TextRenderSystem"/> resolves, lays out glyphs, and submits each frame. Pair with
/// <see cref="Transform"/> for baseline-left.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="CoordinateSpace.WorldSpace"/> for diegetic labels in the playfield;
/// use <see cref="CoordinateSpace.PresentationViewportSpace"/> for screen-locked HUD (stable size when the camera zoom
/// changes <see cref="Camera2D.ViewportSizeWorld"/>; set <see cref="Camera2D.PresentationViewportSizeWorld"/> to your design canvas).
/// <see cref="CoordinateSpace.ViewportSpace"/> keeps text tied to the zoomed virtual viewport.
/// Screen-space rows often pair with <see cref="ViewportAnchor2D"/> so <see cref="Transform.WorldPosition"/> tracks resize.
/// </para>
/// <para>
/// For retained-mode UI (panels, buttons, multi-line wrapped copy), use <see cref="UI.Text.UiTextBlock"/> inside a
/// <see cref="UI.Core.UiDocument"/> driven by <see cref="Systems.UiDocumentFrameSystem"/> instead of attaching this component.
/// </para>
/// <para>
/// Recommended <see cref="SortKey"/> bands: lower values for world text, higher (e.g. 400+) for UI so HUD stacks above gameplay.
/// </para>
/// <para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures
/// <see cref="Transform"/>, <see cref="TextBuildFingerprint"/>, and <see cref="TextSpriteCache"/> exist on the same entity
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </para>
/// </remarks>
[RequiresComponent<Transform>]
[RequiresComponent<TextBuildFingerprint>]
[RequiresComponent<TextSpriteCache>]
public struct BitmapText : IComponent
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

    /// <summary>Whether <see cref="Transform.WorldPosition"/> is world (+Y up) or virtual canvas pixels (+Y down).</summary>
    public CoordinateSpace CoordinateSpace;

    /// <summary>
    /// Default for new HUD rows: <see cref="CoordinateSpace.PresentationViewportSpace"/> (stable on-screen size across camera zoom).
    /// </summary>
    public static CoordinateSpace HudDefaultCoordinateSpace => CoordinateSpace.PresentationViewportSpace;
}
