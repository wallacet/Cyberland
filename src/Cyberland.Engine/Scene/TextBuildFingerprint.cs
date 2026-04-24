using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Snapshot of inputs used to decide whether cached glyph quads for a <see cref="BitmapText"/> row need rebuilding.
/// </summary>
/// <remarks>
/// Written by <see cref="Systems.TextBuildSystem"/> and read by <see cref="Systems.TextRenderSystem"/>. Baseline values are
/// derived from <see cref="Transform"/> each frame; this struct stores the last-used baseline for comparison only.
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct TextBuildFingerprint : IComponent
{
    /// <summary>Ordinal hash of the resolved display string from the last successful glyph build.</summary>
    public int ResolvedContentHash;

    /// <summary>Hash of <see cref="BitmapText.Style"/> from the last successful glyph build.</summary>
    public int StyleHash;

    /// <summary><see cref="BitmapText.CoordinateSpace"/> at the last successful glyph build.</summary>
    public CoordinateSpace CoordinateSpace;

    /// <summary><see cref="BitmapText.SortKey"/> at the last successful glyph build.</summary>
    public float SortKey;

    /// <summary>World-space baseline X used for the last successful glyph build.</summary>
    public float BaselineWorldX;

    /// <summary>World-space baseline Y used for the last successful glyph build.</summary>
    public float BaselineWorldY;

    /// <summary>Framebuffer width in pixels when <see cref="BitmapText.CoordinateSpace"/> was screen space; otherwise 0.</summary>
    public int FramebufferW;

    /// <summary>Framebuffer height in pixels when <see cref="BitmapText.CoordinateSpace"/> was screen space; otherwise 0.</summary>
    public int FramebufferH;
}
