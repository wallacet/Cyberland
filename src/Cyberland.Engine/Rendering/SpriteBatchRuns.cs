namespace Cyberland.Engine.Rendering;

/// <summary>
/// Contiguous-run batch keys for instanced sprite draws: same rules as legacy per-draw binds (texture pairs + clip).
/// </summary>
internal static class SpriteBatchRuns
{
    /// <summary>
    /// Chooses the normal-map texture id for deferred opaque and swapchain overlay sprite batches.
    /// Matches GPU binding in <c>VulkanRenderer.Deferred.Recording</c>: use the requested slot when it exists,
    /// otherwise fall back to the renderer default flat normal (same rule as MaxValue / missing registration).
    /// </summary>
    /// <remarks>
    /// When <see cref="SpriteDrawRequest.NormalTextureId"/> equals <see cref="SpriteDrawRequest.AlbedoTextureId"/>,
    /// the same GPU texture would be sampled as both albedo and tangent normal. Slot 0 is usually the white albedo;
    /// treating it as a normal map corrupts G-buffer normals and deferred lighting can erase visible shading even
    /// though instanced draws still execute — a common partial-authoring footgun (<c>default</c> ids or copy mistakes).
    /// </remarks>
    public static TextureId EffectiveNormalTextureIdForDeferredSprite(
        in SpriteDrawRequest s,
        TextureId defaultNormalTextureId,
        bool requestedNormalSlotExists)
    {
        if (s.NormalTextureId == s.AlbedoTextureId)
            return defaultNormalTextureId;
        return requestedNormalSlotExists ? s.NormalTextureId : defaultNormalTextureId;
    }

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
        prev.Space == next.Space &&
        (!prev.ViewportClipEnabled || prev.ViewportClipRect.Equals(next.ViewportClipRect));
}
