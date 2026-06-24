using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Controls;

/// <summary>Texture sprite leaf stretched to <see cref="UiElement.ComputedBounds"/>.</summary>
public class UiImage : UiElement
{
    /// <summary>Albedo slot to draw; <see cref="TextureId.MaxValue"/> skips submission.</summary>
    public TextureId SourceTextureId { get; set; } = TextureId.MaxValue;

    /// <summary>UV rect within <see cref="SourceTextureId"/> (atlas regions use sub-rects).</summary>
    public Vector4D<float> UvRect { get; set; }

    /// <summary>Source pixel size for 9-slice math (atlas region or loaded PNG dimensions).</summary>
    public int SourcePixelWidth { get; set; } = 1;

    /// <summary>Source pixel height for 9-slice math.</summary>
    public int SourcePixelHeight { get; set; } = 1;

    /// <summary>Optional nine-slice insets overriding atlas region defaults.</summary>
    public NineSliceInsets NineSlice { get; set; }

    /// <summary>Straight RGBA tint multiplied into samples.</summary>
    public Vector4D<float> Tint { get; set; } = new(1f, 1f, 1f, 1f);

    /// <inheritdoc />
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey,
        in UiRect viewportClip,
        in UiRect inheritedClip)
    {
        _ = fonts;
        _ = cache;
        _ = inheritedClip;
        if (SourceTextureId == TextureId.MaxValue || Tint.W <= 1e-4f)
            return;

        if (viewportClip.Width <= 1e-4f || viewportClip.Height <= 1e-4f)
            return;

        var uv = UvRect;
        if (uv == default)
            uv = new Vector4D<float>(0f, 0f, 1f, 1f);

        if (NineSlice.IsEmpty)
        {
            UiVisualSubmission.SubmitFilledQuad(
                renderer, ComputedBounds, space, SourceTextureId, Tint, sortKey, viewportClip, uv);
            return;
        }

        Span<NineSliceLayout.SliceQuad> slices = stackalloc NineSliceLayout.SliceQuad[9];
        var count = NineSliceLayout.BuildQuads(
            ComputedBounds, uv, SourcePixelWidth, SourcePixelHeight, NineSlice, slices);
        if (count <= 0)
        {
            UiVisualSubmission.SubmitFilledQuad(
                renderer, ComputedBounds, space, SourceTextureId, Tint, sortKey, viewportClip, uv);
            return;
        }

        UiVisualSubmission.SubmitSliceQuads(renderer, slices[..count], space, SourceTextureId, Tint, sortKey, viewportClip);
    }
}
