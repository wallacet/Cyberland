using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Cached glyph sprite quads for one <see cref="BitmapText"/> row, produced by <see cref="Systems.TextBuildSystem"/>.
/// </summary>
/// <remarks>
/// Submitted by <see cref="Systems.TextRenderSystem"/> in stable query order. Decoration lines use <see cref="PenAfter"/>
/// from the same glyph layout pass.
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> and
/// <see cref="TextBuildFingerprint"/> exist (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
[RequiresComponent<TextBuildFingerprint>]
public struct TextSpriteCache : IComponent
{
    /// <summary>Pen position after laying out the cached glyph run (world units along the row).</summary>
    public float PenAfter;

    /// <summary>Backing storage for cached <see cref="SpriteDrawRequest"/> values; may be longer than <see cref="GlyphCount"/>.</summary>
    public SpriteDrawRequest[]? CachedGlyphs;

    /// <summary>Number of valid entries in <see cref="CachedGlyphs"/> for the current cached run.</summary>
    public int GlyphCount;
}
