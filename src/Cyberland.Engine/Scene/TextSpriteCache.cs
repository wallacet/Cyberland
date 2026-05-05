using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// CPU cache of <see cref="SpriteDrawRequest"/> quads for one <see cref="BitmapText"/> row, filled by
/// <see cref="Systems.TextRuntimeBuilder"/> immediately before <see cref="Rendering.IRenderer.SubmitSprites"/> in
/// <see cref="Systems.TextRenderSystem"/>.
/// </summary>
/// <remarks>
/// <see cref="GlyphCount"/> is the only length the renderer should use for this row; the backing array may be longer.
/// Underlines / strike use <see cref="PenAfter"/> (pen in pixels), which can extend past the last *drawn* glyph if a
/// codepoint failed the glyph cache / raster path and only advanced the pen.
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> and
/// <see cref="TextBuildFingerprint"/> exist (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
[RequiresComponent<TextBuildFingerprint>]
public struct TextSpriteCache : IComponent
{
    /// <summary>Pen position after laying out the cached glyph run (world units along the row).</summary>
    public float PenAfter;

    /// <summary>
    /// Grow-only backing array for shaped quads; its length is <strong>capacity</strong>, not “glyphs on screen.”
    /// Only indices <c>[0 .. GlyphCount)</c> are valid after a successful prepare; after <see cref="Systems.TextRuntimeBuilder.DiscardGlyphCache"/>,
    /// this may be <c>null</c> until the next layout.
    /// </summary>
    public SpriteDrawRequest[]? CachedGlyphs;

    /// <summary>Number of valid entries in <see cref="CachedGlyphs"/> for the current cached run.</summary>
    public int GlyphCount;

    /// <summary>Last authored baseline used to build the cached glyph run.</summary>
    public Vector2D<float> BaselineAuthored;

    /// <summary>Coordinate space used by the current cached glyph run.</summary>
    public CoordinateSpace Space;
}
