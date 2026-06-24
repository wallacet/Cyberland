using Cyberland.Engine.Assets;
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
        in UiRect viewportClip,
        in Vector4D<float> uvRect = default)
    {
        var cx = rect.X + rect.Width * 0.5f;
        var cy = rect.Y + rect.Height * 0.5f;
        var straightA = colorMultiply.W;
        var clipScreen = space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace or CoordinateSpace.SwapchainSpace;
        var uv = uvRect == default ? new Vector4D<float>(0f, 0f, 1f, 1f) : uvRect;
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
            UvRect = uv,
            Transparent = straightA < 0.999f,
            Space = space,
            ViewportClipEnabled = clipScreen,
            ViewportClipRect = viewportClip
        });
    }

    public static void SubmitSliceQuads(
        IRenderer renderer,
        ReadOnlySpan<NineSliceLayout.SliceQuad> slices,
        CoordinateSpace space,
        TextureId albedoTextureId,
        in Vector4D<float> colorMultiply,
        float sortKey,
        in UiRect viewportClip)
    {
        var straightA = colorMultiply.W;
        var clipScreen = space is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace or CoordinateSpace.SwapchainSpace;
        foreach (ref readonly var slice in slices)
        {
            renderer.SubmitSprite(new SpriteDrawRequest
            {
                CenterWorld = slice.Center,
                HalfExtentsWorld = slice.HalfExtents,
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
                UvRect = slice.UvRect,
                Transparent = straightA < 0.999f,
                Space = space,
                ViewportClipEnabled = clipScreen,
                ViewportClipRect = viewportClip
            });
        }
    }
}
