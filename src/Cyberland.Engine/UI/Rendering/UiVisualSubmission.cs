using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Core;
using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Rendering;

/// <summary>
/// Shared axis-aligned UI quads (viewport/world space +Y down / engine conventions).
/// </summary>
internal static class UiVisualSubmission
{
    public static void SubmitFilledQuad(
        IRenderer renderer,
        in UiRect rect,
        CoordinateSpace space,
        TextureId albedoTextureId,
        in Vector4D<float> colorMultiply,
        float sortKey,
        in UiRect viewportClip)
    {
        var cx = rect.X + rect.Width * 0.5f;
        var cy = rect.Y + rect.Height * 0.5f;
        var straightA = colorMultiply.W;
        var clipScreen = space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace or CoordinateSpace.SwapchainSpace;
        renderer.SubmitSprite(new SpriteDrawRequest
        {
            CenterWorld = new Vector2D<float>(cx, cy),
            HalfExtentsWorld = new Vector2D<float>(rect.Width * 0.5f, rect.Height * 0.5f),
            RotationRadians = 0f,
            Layer = (int)SpriteLayer.Ui,
            SortKey = sortKey,
            AlbedoTextureId = albedoTextureId,
            NormalTextureId = renderer.DefaultNormalTextureId,
            EmissiveTextureId = TextureId.MaxValue,
            ColorMultiply = colorMultiply,
            Alpha = straightA,
            EmissiveTint = default,
            EmissiveIntensity = 0f,
            DepthHint = 0f,
            UvRect = default,
            Transparent = straightA < 0.999f,
            Space = space,
            ViewportClipEnabled = clipScreen,
            ViewportClipRect = viewportClip
        });
    }
}
