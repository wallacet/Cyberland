using System;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

internal static class TextRuntimeBuilder
{
    public static bool TryPrepare(
        ref BitmapText bt,
        ref TextBuildFingerprint fingerprint,
        ref TextSpriteCache cache,
        ref readonly Transform transform,
        GameHostServices host,
        IRenderer renderer,
        out Vector2D<float> baselineAuthored,
        out CoordinateSpace space)
    {
        baselineAuthored = default;
        space = CoordinateSpace.WorldSpace;
        if (!ValidateAndResolve(ref bt, host, out var resolved))
            return false;

        ResolveBaseline(in bt, in transform, host, renderer, out baselineAuthored, out space, out var vpW, out var vpH);

        var contentHash = HashResolvedContent64(resolved);
        var styleHash = bt.Style.GetHashCode();
        var unchanged =
            fingerprint.ResolvedContentHash64 == contentHash &&
            fingerprint.StyleHash == styleHash &&
            fingerprint.CoordinateSpace == bt.CoordinateSpace &&
            fingerprint.SortKey == bt.SortKey &&
            fingerprint.FramebufferW == vpW &&
            fingerprint.FramebufferH == vpH;
        cache.BaselineAuthored = baselineAuthored;
        cache.Space = space;
        if (unchanged)
            return true;

        fingerprint.ResolvedContentHash64 = contentHash;
        fingerprint.StyleHash = styleHash;
        fingerprint.CoordinateSpace = bt.CoordinateSpace;
        fingerprint.SortKey = bt.SortKey;
        fingerprint.FramebufferW = vpW;
        fingerprint.FramebufferH = vpH;

        BuildGlyphSprites(ref bt, ref cache, host, renderer, resolved, baselineAuthored, space);
        return true;
    }

    private static ulong HashResolvedContent64(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        for (var i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return hash;
    }

    private static bool ValidateAndResolve(ref BitmapText bt, GameHostServices host, out string resolved)
    {
        if (!bt.Visible || string.IsNullOrEmpty(bt.Content))
        {
            resolved = string.Empty;
            return false;
        }

        if (!bt.IsLocalizationKey)
        {
            resolved = bt.Content;
            return true;
        }

        var localization = host.LocalizedContent?.Strings;
        if (localization is null)
        {
            resolved = string.Empty;
            return false;
        }

        resolved = localization.Get(bt.Content);
        if (!string.IsNullOrEmpty(resolved))
            return true;

        resolved = string.Empty;
        return false;
    }

    private static void ResolveBaseline(
        ref readonly BitmapText bt,
        ref readonly Transform transform,
        GameHostServices host,
        IRenderer renderer,
        out Vector2D<float> baselineAuthored,
        out CoordinateSpace space,
        out int vpW,
        out int vpH)
    {
        // Translation is the homogeneous matrix's third row (M31, M32); pull it directly to avoid a decompose just to
        // recover the world position.
        var worldTranslation = new Vector2D<float>(transform.WorldMatrix.M31, transform.WorldMatrix.M32);
        if (bt.CoordinateSpace == CoordinateSpace.WorldSpace)
        {
            baselineAuthored = worldTranslation;
            space = CoordinateSpace.WorldSpace;
            vpW = 0;
            vpH = 0;
            return;
        }

        // Screen-space text: author in the camera's virtual viewport (so HUD size/layout is independent of the
        // physical window). Glyphs submit in viewport space; the fingerprint tracks viewport extent so HUD
        // rebuilds when the active camera swaps or its virtual viewport changes.
        baselineAuthored = worldTranslation;
        space = CoordinateSpace.ViewportSpace;
        var viewport = host.CameraRuntimeState.ViewportSizeWorld;
        vpW = viewport.X;
        vpH = viewport.Y;
    }

    private static void BuildGlyphSprites(
        ref BitmapText bt,
        ref TextSpriteCache cache,
        GameHostServices host,
        IRenderer renderer,
        string resolved,
        Vector2D<float> baselineAuthored,
        CoordinateSpace space)
    {
        var needed = Math.Max(1, resolved.Length);
        if (cache.CachedGlyphs is null || cache.CachedGlyphs.Length < needed)
            cache.CachedGlyphs = new SpriteDrawRequest[needed];

        var n = TextRenderer.FillGlyphRunSprites(
            renderer,
            host.Fonts,
            host.TextGlyphCache,
            resolved,
            in bt.Style,
            baselineAuthored,
            0f,
            bt.SortKey,
            cache.CachedGlyphs.AsSpan(0, needed),
            out var penAfter,
            space);
        cache.GlyphCount = n;
        cache.PenAfter = penAfter;
    }
}
