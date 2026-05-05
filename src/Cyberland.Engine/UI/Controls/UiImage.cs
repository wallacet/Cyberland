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

    /// <summary>Straight RGBA tint multiplied into samples.</summary>
    public Vector4D<float> Tint { get; set; } = new(1f, 1f, 1f, 1f);

    /// <inheritdoc />
    protected override void DrawSelfVisuals(
        IRenderer renderer,
        FontLibrary fonts,
        TextGlyphCache cache,
        CoordinateSpace space,
        float sortKey)
    {
        _ = fonts;
        _ = cache;
        if (SourceTextureId == TextureId.MaxValue || Tint.W <= 1e-4f)
            return;

        UiVisualSubmission.SubmitFilledQuad(renderer, ComputedBounds, space, SourceTextureId, Tint, sortKey);
    }
}
