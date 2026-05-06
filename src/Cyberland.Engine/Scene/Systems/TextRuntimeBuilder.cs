// BitmapText pipeline (CPU): resolve string → layout glyph quads into TextSpriteCache → TextRenderSystem submits the same
// frame. Fast-path reuse is allowed only when all layout-driving inputs, baseline, and cached glyph buffers still match.
// Any mismatch falls back to full rebuild to avoid stale GlyphCount / resolved-string skew bugs.
using System;
using System.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>Builds <see cref="TextSpriteCache"/> for one <see cref="BitmapText"/> row; <see cref="TextRenderSystem"/> is the only production caller.</summary>
internal static class TextRuntimeBuilder
{
    public static bool TryPrepare(
        ref BitmapText bt,
        ref TextBuildFingerprint fingerprint,
        ref TextSpriteCache cache,
        ref readonly Transform transform,
        GameHostServices host,
        IRenderer? renderer,
        out Vector2D<float> baselineAuthored,
        out CoordinateSpace space)
    {
        baselineAuthored = default;
        space = CoordinateSpace.WorldSpace;

        if (renderer is null)
        {
            // Host skips TextRenderSystem when IRenderer is missing; direct TryPrepare callers (tests) must not NRE.
            DiscardGlyphCache(ref cache);
            fingerprint = default;
            return false;
        }

        if (!ValidateAndResolve(ref bt, host, out var resolved))
        {
            // Row invisible, empty, or localization miss — do not leave last frame's quads or counts live.
            DiscardGlyphCache(ref cache);
            fingerprint = default;
            return false;
        }

        ResolveBaseline(in bt, in transform, host, out baselineAuthored, out space, out var vpW, out var vpH);

        cache.BaselineAuthored = baselineAuthored;
        cache.Space = space;

        // If resolved copy / style / layer inputs changed vs the last successful prepare, drop CPU glyph slots + reset the
        // fingerprint before rebuilding so nothing addressable looks like a longer run (mods should not need to call
        // DiscardPrepared for normal BitmapText.Content edits). Baseline/transform-only moves intentionally do not match here,
        // so HUD drift does not thrash the backing array every frame — BuildGlyphSprites still runs with the new baseline.
        if (!ContentPipelineInputsMatchLastPrepare(in fingerprint, in bt, resolved, vpW, vpH))
            DiscardPreparedRow(ref cache, ref fingerprint);

        if (CanReusePreparedGlyphs(in fingerprint, in cache, in bt, resolved, baselineAuthored, vpW, vpH))
            return true;

        // Fingerprint must reflect a completed glyph fill — never expose new ResolvedCharCount/hash alongside a prior
        // GlyphCount if BuildGlyphSprites were ever to fail mid-flight (future-proof ordering).
        BuildGlyphSprites(ref bt, ref cache, host, renderer, resolved, baselineAuthored, space);
        WriteFingerprintFromPrepare(ref fingerprint, in bt, resolved, baselineAuthored, vpW, vpH);
        return true;
    }

    /// <summary>Public entry for <see cref="BitmapTextPreparedRow"/> — clears GPU-bound sprite cache and fingerprint together.</summary>
    internal static void DiscardPreparedRow(ref TextSpriteCache cache, ref TextBuildFingerprint fingerprint)
    {
        DiscardGlyphCache(ref cache);
        fingerprint = default;
    }

    /// <summary>Clears cached glyph quads when the row cannot draw or is turned off.</summary>
    /// <remarks>
    /// Drops <see cref="TextSpriteCache.CachedGlyphs"/> entirely so allocation capacity cannot be mistaken for “number of
    /// glyphs” in diagnostics; the next successful prepare allocates for the current resolved string length only.
    /// </remarks>
    internal static void DiscardGlyphCache(ref TextSpriteCache cache)
    {
        cache.GlyphCount = 0;
        cache.PenAfter = 0f;
        cache.CachedGlyphs = null;
    }

    /// <summary>
    /// Diagnostic / test helper: clamp glyph submissions to resolved UTF-16 length.
    /// After a successful <see cref="TryPrepare"/>, <see cref="TextSpriteCache.GlyphCount"/> is authoritative (fill uses the
    /// same resolved string); do not gate submission on <see cref="TextBuildFingerprint.ResolvedCharCount"/> alone — if the
    /// fingerprint was cleared without a matching cache discard, it could be 0 and incorrectly suppress all submits.
    /// </summary>
    internal static int SubmitSpriteCount(in TextSpriteCache cache, int resolvedUtf16LengthFromPrepare) =>
        resolvedUtf16LengthFromPrepare <= 0 ? 0 : Math.Min(cache.GlyphCount, resolvedUtf16LengthFromPrepare);

    private static void WriteFingerprintFromPrepare(
        ref TextBuildFingerprint fingerprint,
        in BitmapText bt,
        string resolved,
        Vector2D<float> baselineAuthored,
        int vpW,
        int vpH)
    {
        fingerprint.ResolvedContentHash64 = HashResolvedContent64(resolved);
        fingerprint.ResolvedCharCount = resolved.Length;
        fingerprint.StyleHash = bt.Style.GetHashCode();
        fingerprint.CoordinateSpace = bt.CoordinateSpace;
        fingerprint.SortKey = bt.SortKey;
        fingerprint.FramebufferW = vpW;
        fingerprint.FramebufferH = vpH;
        fingerprint.BaselineWorldX = baselineAuthored.X;
        fingerprint.BaselineWorldY = baselineAuthored.Y;
    }

    /// <summary>
    /// Compares stored fingerprint to the row inputs that define glyph layout (resolved UTF-16, style, layer, viewport).
    /// Excludes baseline position — moving text updates quads without requiring a cache discard.
    /// </summary>
    private static bool ContentPipelineInputsMatchLastPrepare(
        in TextBuildFingerprint fp,
        in BitmapText bt,
        string resolved,
        int vpW,
        int vpH)
    {
        if (fp.ResolvedCharCount != resolved.Length)
            return false;
        if (fp.ResolvedContentHash64 != HashResolvedContent64(resolved))
            return false;
        if (fp.StyleHash != bt.Style.GetHashCode())
            return false;
        if (fp.CoordinateSpace != bt.CoordinateSpace)
            return false;
        if (fp.SortKey != bt.SortKey)
            return false;
        if (fp.FramebufferW != vpW || fp.FramebufferH != vpH)
            return false;
        return true;
    }

    private static bool CanReusePreparedGlyphs(
        in TextBuildFingerprint fingerprint,
        in TextSpriteCache cache,
        in BitmapText text,
        string resolved,
        Vector2D<float> baselineAuthored,
        int vpW,
        int vpH)
    {
        if (!ContentPipelineInputsMatchLastPrepare(in fingerprint, in text, resolved, vpW, vpH))
            return false;
        if (!BaselineMatches(in fingerprint, baselineAuthored))
            return false;
        if (cache.CachedGlyphs is null)
            return false;
        if (cache.GlyphCount < 0 || cache.GlyphCount > resolved.Length)
            return false;
        return true;
    }

    private static bool BaselineMatches(in TextBuildFingerprint fingerprint, in Vector2D<float> baselineAuthored) =>
        fingerprint.BaselineWorldX == baselineAuthored.X &&
        fingerprint.BaselineWorldY == baselineAuthored.Y;

    private static ulong HashResolvedContent64(string value)
    {
        // FNV-1a 64: stable, cheap; used for diagnostics / future diffing — not a security hash.
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        hash ^= (ulong)(uint)value.Length;
        hash *= prime;
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
        out Vector2D<float> baselineAuthored,
        out CoordinateSpace space,
        out int vpW,
        out int vpH)
    {
        // World translation is the 2D translation row of WorldMatrix (M31, M32) — same as anchor parents for HUD.
        var worldTranslation = new Vector2D<float>(transform.WorldMatrix.M31, transform.WorldMatrix.M32);
        if (bt.CoordinateSpace == CoordinateSpace.WorldSpace)
        {
            baselineAuthored = worldTranslation;
            space = CoordinateSpace.WorldSpace;
            vpW = 0;
            vpH = 0;
            return;
        }

        // Viewport / local / swapchain: baseline still comes from the full world matrix (parented anchors, etc.).
        // Never remap CoordinateSpace here — TextRenderer and the Vulkan overlay path use BitmapText.CoordinateSpace
        // for Y sign and for routing to the swapchain UI queue.
        baselineAuthored = worldTranslation;
        space = bt.CoordinateSpace;
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
        var cap = Math.Max(1, resolved.Length);
        if (cache.CachedGlyphs is null || cache.CachedGlyphs.Length < cap)
            cache.CachedGlyphs = new TextGlyphDrawRequest[cap];
        else if (cache.CachedGlyphs.Length > cap * 2)
        {
            // Shrank a lot: drop the large backing array so the tail cannot hold addressable stale cached glyph data
            // values (long tutorial string → short line).
            cache.CachedGlyphs = new TextGlyphDrawRequest[cap];
        }

        cache.CachedGlyphs.AsSpan(0, cap).Clear();

        var fillDest = cache.CachedGlyphs.AsSpan(0, cap);
        Debug.Assert(fillDest.Length == resolved.Length, "BitmapText glyph buffer must match resolved UTF-16 length.");
        var n = TextRenderer.FillGlyphRunGlyphs(
            renderer,
            host.Fonts,
            host.TextGlyphCache,
            resolved,
            in bt.Style,
            baselineAuthored,
            0f,
            bt.SortKey,
            fillDest,
            out var penAfter,
            space);

        // Invariant: one quad per Rune at most; rune count never exceeds UTF-16 length, so n <= cap == resolved.Length.
        Debug.Assert(n <= cap, "FillGlyphRunGlyphs must not outgrow the resolved string capacity.");
        cache.GlyphCount = n;
        cache.PenAfter = penAfter;

        if (n < cache.CachedGlyphs.Length)
            Array.Clear(cache.CachedGlyphs, n, cache.CachedGlyphs.Length - n);
    }
}
