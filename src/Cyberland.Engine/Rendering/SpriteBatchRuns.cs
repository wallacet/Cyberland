namespace Cyberland.Engine.Rendering;

/// <summary>
/// Contiguous-run batch keys for instanced sprite draws: same rules as legacy per-draw binds (texture pairs + clip).
/// </summary>
internal static class SpriteBatchRuns
{
    public static TextureId ResolveNormalTextureId(in SpriteDrawRequest s, TextureId defaultNormalTextureId) =>
        s.NormalTextureId != TextureId.MaxValue ? s.NormalTextureId : defaultNormalTextureId;

    public static bool DeferredEmissiveRunCanExtend(
        in SpriteDrawRequest prev,
        in SpriteDrawRequest next,
        TextureId prevEmissiveSlot,
        TextureId nextEmissiveSlot,
        int prevUseEm,
        int nextUseEm) =>
        prev.AlbedoTextureId == next.AlbedoTextureId &&
        prevEmissiveSlot == nextEmissiveSlot &&
        prevUseEm == nextUseEm;

    public static bool DeferredOpaqueRunCanExtend(
        in SpriteDrawRequest prev,
        in SpriteDrawRequest next,
        TextureId prevNormResolved,
        TextureId nextNormResolved) =>
        prev.AlbedoTextureId == next.AlbedoTextureId && prevNormResolved == nextNormResolved;

    public static bool DeferredTransparentRunCanExtend(in SpriteDrawRequest prev, in SpriteDrawRequest next) =>
        prev.AlbedoTextureId == next.AlbedoTextureId;

    public static bool OverlayRunCanExtend(
        in SpriteDrawRequest prev,
        in SpriteDrawRequest next,
        TextureId prevNormResolved,
        TextureId nextNormResolved) =>
        prev.AlbedoTextureId == next.AlbedoTextureId &&
        prevNormResolved == nextNormResolved &&
        prev.ViewportClipEnabled == next.ViewportClipEnabled &&
        (!prev.ViewportClipEnabled || prev.ViewportClipRect.Equals(next.ViewportClipRect));
}
