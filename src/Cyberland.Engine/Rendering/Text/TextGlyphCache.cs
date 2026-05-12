using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using SixLabors.Fonts;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Glyph bitmap cache backed by packed RGBA atlas pages (one GPU texture per page, sub-rectangle updates for new ink).
/// </summary>
/// <remarks>
/// <para>
/// Cache map and atlas packing are synchronized under an internal lock. Rasterization happens outside the cache lock
/// (still synchronized through <see cref="FontLibrary.FontRasterSync"/>) so unrelated glyph misses do not block each
/// other on CPU MSDF generation. <see cref="Clear"/> drops CPU glyph maps; atlas GPU textures remain resident until process
/// teardown unless a future renderer API releases slots. Shared measured/raster <c>SixLabors.Fonts.Font</c> instances live on
/// <see cref="FontLibrary"/> (same quantization as glyph keys).
/// </para>
/// <para>
/// Face/size keys use <see cref="FontLibrary.QuantizeEmSizePixels"/> so jittery float sizes map to stable buckets.
/// </para>
/// <para>
/// In Debug builds, optional console warnings (enabled from <see cref="GameApplication"/> startup) fire once per distinct
/// glyph key when this cache misses baked entries and falls back to runtime MSDF rasterization — shipped titles should rely on
/// baked atlases instead.
/// </para>
/// </remarks>
public sealed class TextGlyphCache
{
    [ExcludeFromCodeCoverage(Justification = "Telemetry snapshot DTO; counters are validated via SnapshotAndResetTelemetry tests.")]
    internal readonly record struct GlyphCacheTelemetry(
        long CacheHits,
        long CacheMisses,
        long UploadCalls,
        long UploadBytes,
        double RasterizeMs,
        double UploadMs);

    private readonly object _lock = new();
    private readonly ConcurrentQueue<RasterJob> _pendingRasterJobs = new();
    private readonly ConcurrentQueue<RasterResult> _completedRasterJobs = new();
    private readonly ConcurrentDictionary<(string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision), byte> _inFlightJobs = new();
    private readonly AutoResetEvent _rasterWake = new(false);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Thread _rasterWorker;
    private static long s_cacheHits;
    private static long s_cacheMisses;
    private static long s_uploadCalls;
    private static long s_uploadBytes;
    private static long s_rasterizeTicks;
    private static long s_uploadTicks;
    private static long s_bakedGlyphImports;
    private static long s_contentVersion;
    private static readonly ConcurrentDictionary<int, long> s_missCodepoints = new();
    private static readonly ConcurrentDictionary<string, long> s_missGlyphKeys = new();

#if DEBUG
    private static readonly ConcurrentDictionary<string, byte> s_msdfFallbackWarnOnceKeys = new();

    /// <summary>
    /// Debug-only: first runtime MSDF raster fallback per distinct miss key logs one console line when true.
    /// Left false for engine unit tests (many deliberate misses); <c>GameApplication</c> sets this at startup.
    /// </summary>
    internal static bool EnableMsdfFallbackConsoleWarnings { get; set; }

    internal static void ClearMsdfFallbackWarnOnceKeysForTests() => s_msdfFallbackWarnOnceKeys.Clear();
#endif

    /// <summary>Cached GPU glyph with layout metrics and atlas UVs (min.xy, max.zw).</summary>
    public readonly struct CachedGlyph
    {
        /// <summary>Atlas page texture id from <see cref="IRenderer.RegisterTextureRgbaLinear"/>.</summary>
        public TextureId TextureId { get; init; }

        /// <summary>Glyph draw width in logical text pixels (before viewport-to-swapchain scaling).</summary>
        public float WidthPx { get; init; }

        /// <summary>Glyph draw height in logical text pixels (before viewport-to-swapchain scaling).</summary>
        public float HeightPx { get; init; }

        /// <summary>Horizontal offset from pen to sprite center (pixels).</summary>
        public float OffsetPenToCenterX { get; init; }

        /// <summary>Vertical offset from baseline world Y to sprite center (world +Y up).</summary>
        public float OffsetPenToCenterYWorld { get; init; }

        /// <summary>Advance to next pen position (pixels).</summary>
        public float AdvancePx { get; init; }

        /// <summary>Normalized UV rectangle (min.xy, max.zw). Zero means full texture (legacy single-glyph textures).</summary>
        public Vector4D<float> UvRect { get; init; }

        /// <summary>Distance normalization range in source atlas pixels for MSDF reconstruction.</summary>
        public float MsdfPixelRange { get; init; }
    }

    // Tuple key avoids unused generated record accessors in coverage for the private key type.
    private readonly Dictionary<(string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision),
        CachedGlyph> _glyphs = new();

    private readonly List<GlyphAtlasPage> _pages = new();

    private readonly record struct RasterJob(
        FontLibrary Fonts,
        TextStyle Style,
        string Glyph,
        (string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision) Key);

    private readonly record struct RasterResult(
        (string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision) Key,
        byte[] Rgba,
        int AtlasWidth,
        int AtlasHeight,
        float DrawWidthPx,
        float DrawHeightPx,
        float AdvancePx,
        float OffsetPenToCenterX,
        float OffsetPenToCenterYWorld,
        float MsdfPixelRange);

    /// <summary>
    /// When true, cache misses are rasterized on a background worker and become visible after a drain tick.
    /// Runtime host enables this to avoid render-thread spikes; unit tests may keep the default synchronous behavior.
    /// </summary>
    public bool UseAsyncRasterization { get; set; }

    /// <summary>Creates a glyph cache and starts the background raster worker.</summary>
    public TextGlyphCache()
    {
        _rasterWorker = new Thread(() => RasterWorkerLoop(_shutdown.Token))
        {
            IsBackground = true,
            Name = "Cyberland.TextGlyphCache.RasterWorker"
        };
        _rasterWorker.Start();
    }

    /// <summary>
    /// Clears CPU-side glyph maps and atlas bookkeeping (does not destroy GPU textures). <see cref="FontLibrary"/> keeps
    /// shared <see cref="Font"/> instances for measure/raster; this does not clear that cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _glyphs.Clear();
            _pages.Clear();
        }
    }

    /// <summary>
    /// Resolves a glyph through the cache, rasterizing and uploading on miss.
    /// </summary>
    /// <returns>False if the family could not be resolved or rasterization failed.</returns>
    public bool TryGetGlyph(
        IRenderer renderer,
        FontLibrary fonts,
        in TextStyle style,
        int codePoint,
        string utf16Glyph,
        out CachedGlyph glyph) =>
        TryGetGlyph(renderer, fonts, in style, codePoint, utf16Glyph.AsSpan(), out glyph);

    /// <summary>Span-based grapheme (1–2 chars) to avoid per-codepoint string allocations on hot paths.</summary>
    public bool TryGetGlyph(
        IRenderer renderer,
        FontLibrary fonts,
        in TextStyle style,
        int codePoint,
        ReadOnlySpan<char> utf16Glyph,
        out CachedGlyph glyph)
    {
        glyph = default;
        if (renderer is null)
            return false;

        if (utf16Glyph.IsEmpty)
            return false;

        if (!fonts.TryGetFamily(style.FontFamilyId, out var fam) || fam is null)
            return false;

        var faceKind = FontLibrary.SelectFace(style.Bold, style.Italic, fam);
        var sizeQ = FontLibrary.QuantizeEmSizePixels(style.SizePixels);
        var key = (style.FontFamilyId, faceKind, sizeQ, codePoint, GlyphRasterizer.RasterRevision);

        lock (_lock)
        {
            if (_glyphs.TryGetValue(key, out var existing))
            {
                Interlocked.Increment(ref s_cacheHits);
                glyph = existing;
                return true;
            }
        }
        Interlocked.Increment(ref s_cacheMisses);
        s_missCodepoints.AddOrUpdate(codePoint, 1, static (_, c) => c + 1);
        var missKey = $"{style.FontFamilyId}|{faceKind}|q{sizeQ}|U+{codePoint:X4}";
        s_missGlyphKeys.AddOrUpdate(missKey, 1, static (_, c) => c + 1);
#if DEBUG
        WarnRuntimeMsdfRasterFallbackOnce(missKey, in style, utf16Glyph);
#endif
        if (!UseAsyncRasterization)
            return TryRasterizeAndUploadSynchronously(renderer, fonts, in style, utf16Glyph, key, out glyph);

        EnqueueRasterJob(fonts, in style, utf16Glyph, key);
        return false;
    }

    /// <summary>
    /// Uploads completed background raster jobs onto the render thread and seeds cache entries.
    /// Call once per render tick before text staging.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Render-thread drain sequencing is validated through runtime integration scenarios.")]
    internal void DrainPendingGlyphUploads(IRenderer renderer)
    {
        while (_completedRasterJobs.TryDequeue(out var completed))
        {
            var key = completed.Key;
            try
            {
                lock (_lock)
                {
                    if (_glyphs.ContainsKey(key))
                        continue;
                    if (!TryPackAndUpload(renderer, completed.Rgba, completed.AtlasWidth, completed.AtlasHeight, out var texId, out var uv))
                        continue;

                    _glyphs[key] = new CachedGlyph
                    {
                        TextureId = texId,
                        WidthPx = completed.DrawWidthPx,
                        HeightPx = completed.DrawHeightPx,
                        OffsetPenToCenterX = completed.OffsetPenToCenterX,
                        OffsetPenToCenterYWorld = completed.OffsetPenToCenterYWorld,
                        AdvancePx = completed.AdvancePx,
                        UvRect = uv,
                        MsdfPixelRange = completed.MsdfPixelRange
                    };
                    Interlocked.Increment(ref s_contentVersion);
                }
            }
            finally
            {
                _inFlightJobs.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Content version increments whenever new glyph data becomes renderable.
    /// Retained UI can watch this to invalidate cached no-ink replay states.
    /// </summary>
    internal static long ContentVersion => Interlocked.Read(ref s_contentVersion);

    /// <summary>Stops the background raster worker.</summary>
    [ExcludeFromCodeCoverage(Justification = "Worker shutdown path is process-lifecycle integration behavior.")]
    public void Shutdown()
    {
        _shutdown.Cancel();
        _rasterWake.Set();
        try
        {
            _ = _rasterWorker.Join(250);
        }
        catch
        {
            // Best-effort shutdown; process teardown ends remaining work.
        }
        finally
        {
            _rasterWake.Dispose();
            _shutdown.Dispose();
        }
    }

    /// <summary>Tests and atlas packing: rejects dimensions that do not fit a fresh 2048² page.</summary>
    internal bool TryPackAndUpload(
        IRenderer renderer,
        byte[] rgba,
        int w,
        int h,
        out TextureId textureId,
        out Vector4D<float> uvRect)
    {
        var uploadStart = Stopwatch.GetTimestamp();
        textureId = TextureId.MaxValue;
        uvRect = default;

        GlyphAtlasPage? page = _pages.Count > 0 ? _pages[^1] : null;
        if (page is null || !page.TryAllocate(w, h, out var ox, out var oy))
        {
            page = new GlyphAtlasPage();
            _pages.Add(page);
            if (!page.TryAllocate(w, h, out ox, out oy))
                return false;
        }

        GlyphAtlasPage.BlitPremultiplied(page.Pixels, GlyphAtlasPage.SizePx, ox, oy, rgba, w, h);

        var invW = 1f / GlyphAtlasPage.SizePx;
        var invH = 1f / GlyphAtlasPage.SizePx;
        uvRect = new Vector4D<float>(ox * invW, oy * invH, (ox + w) * invW, (oy + h) * invH);

        if (page.TextureId == TextureId.MaxValue)
        {
            var id = renderer.RegisterTextureRgbaLinear(page.Pixels, GlyphAtlasPage.SizePx, GlyphAtlasPage.SizePx);
            if (id == TextureId.MaxValue)
                return false;
            page.TextureId = id;
            textureId = id;
            Interlocked.Increment(ref s_uploadCalls);
            Interlocked.Add(ref s_uploadBytes, (long)GlyphAtlasPage.SizePx * GlyphAtlasPage.SizePx * 4L);
            Interlocked.Add(ref s_uploadTicks, Stopwatch.GetTimestamp() - uploadStart);
            return true;
        }

        if (!renderer.TryUploadTextureRgbaSubregion(page.TextureId, ox, oy, w, h, rgba))
            return false;
        textureId = page.TextureId;
        Interlocked.Increment(ref s_uploadCalls);
        Interlocked.Add(ref s_uploadBytes, (long)w * h * 4L);
        Interlocked.Add(ref s_uploadTicks, Stopwatch.GetTimestamp() - uploadStart);
        return true;
    }

    internal void RegisterBakedGlyph(
        string familyId,
        FontFaceKind face,
        int sizeQuant,
        int codePoint,
        int rasterRevision,
        in CachedGlyph glyph)
    {
        var key = (familyId, face, sizeQuant, codePoint, rasterRevision);
        lock (_lock)
        {
            _glyphs[key] = glyph;
        }
        Interlocked.Increment(ref s_bakedGlyphImports);
        Interlocked.Increment(ref s_contentVersion);
    }

    internal static GlyphCacheTelemetry SnapshotAndResetTelemetry()
    {
        var hits = Interlocked.Exchange(ref s_cacheHits, 0);
        var misses = Interlocked.Exchange(ref s_cacheMisses, 0);
        var uploadCalls = Interlocked.Exchange(ref s_uploadCalls, 0);
        var uploadBytes = Interlocked.Exchange(ref s_uploadBytes, 0);
        var rasterTicks = Interlocked.Exchange(ref s_rasterizeTicks, 0);
        var uploadTicks = Interlocked.Exchange(ref s_uploadTicks, 0);
        _ = Interlocked.Exchange(ref s_bakedGlyphImports, 0);
        return new GlyphCacheTelemetry(
            hits,
            misses,
            uploadCalls,
            uploadBytes,
            rasterTicks * 1000d / Stopwatch.Frequency,
            uploadTicks * 1000d / Stopwatch.Frequency);
    }

    internal static long SnapshotAndResetBakedImportCount() =>
        Interlocked.Exchange(ref s_bakedGlyphImports, 0);

    internal static string SnapshotAndResetMissCodepointSummary(int topN = 12)
    {
        var pairs = s_missCodepoints.ToArray();
        s_missCodepoints.Clear();
        if (pairs.Length == 0)
            return "none";
        Array.Sort(pairs, static (a, b) => b.Value.CompareTo(a.Value));
        var sb = new System.Text.StringBuilder(96);
        var limit = Math.Min(topN, pairs.Length);
        for (var i = 0; i < limit; i++)
        {
            if (i > 0)
                sb.Append(", ");
            var cp = pairs[i].Key;
            sb.Append("U+").Append(cp.ToString("X4")).Append(":").Append(pairs[i].Value);
        }

        return sb.ToString();
    }

    internal static string SnapshotAndResetMissGlyphKeySummary(int topN = 12)
    {
        var pairs = s_missGlyphKeys.ToArray();
        s_missGlyphKeys.Clear();
        if (pairs.Length == 0)
            return "none";
        Array.Sort(pairs, static (a, b) => b.Value.CompareTo(a.Value));
        var sb = new System.Text.StringBuilder(128);
        var limit = Math.Min(topN, pairs.Length);
        for (var i = 0; i < limit; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(pairs[i].Key).Append(":").Append(pairs[i].Value);
        }

        return sb.ToString();
    }

#if DEBUG
    private static void WarnRuntimeMsdfRasterFallbackOnce(string missKey, in TextStyle style, ReadOnlySpan<char> utf16Glyph)
    {
        if (!EnableMsdfFallbackConsoleWarnings)
            return;
        if (!s_msdfFallbackWarnOnceKeys.TryAdd(missKey, 0))
            return;

        var grapheme = utf16Glyph.Length <= 64
            ? utf16Glyph.ToString()
            : string.Concat(utf16Glyph.Slice(0, 64).ToString(), "...");
        grapheme = grapheme.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');

        Console.WriteLine(
            $"Cyberland | MSDF atlas miss — runtime raster fallback | {missKey} | sizePx={style.SizePixels.ToString(CultureInfo.InvariantCulture)} bold={style.Bold} italic={style.Italic} grapheme=\"{grapheme}\" (prefer baked MSDF atlases or align TextStyle with shipped BuiltinFonts coverage)");
    }
#endif

    private void EnqueueRasterJob(
        FontLibrary fonts,
        in TextStyle style,
        ReadOnlySpan<char> utf16Glyph,
        (string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision) key)
    {
        if (!_inFlightJobs.TryAdd(key, 0))
            return;

        _pendingRasterJobs.Enqueue(new RasterJob(fonts, style, utf16Glyph.ToString(), key));
        _rasterWake.Set();
    }

    [ExcludeFromCodeCoverage(Justification = "Background loop scheduling is timing-sensitive and covered by integration runs.")]
    private void RasterWorkerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_pendingRasterJobs.TryDequeue(out var job))
            {
                _rasterWake.WaitOne(16);
                continue;
            }

            try
            {
                var rasterStart = Stopwatch.GetTimestamp();
                if (!job.Fonts.TryCreateTransientFont(job.Style, out var transientFont, out _))
                {
                    _inFlightJobs.TryRemove(job.Key, out _);
                    continue;
                }

                if (!GlyphRasterizer.TryCreateGlyphMsdf(
                    transientFont,
                    job.Glyph,
                    out var rgba,
                    out var atlasW,
                    out var atlasH,
                    out var drawW,
                    out var drawH,
                    out var adv,
                    out var cx,
                    out var cyW,
                    out var msdfRange))
                {
                    _inFlightJobs.TryRemove(job.Key, out _);
                    continue;
                }

                Interlocked.Add(ref s_rasterizeTicks, Stopwatch.GetTimestamp() - rasterStart);
                _completedRasterJobs.Enqueue(new RasterResult(
                    job.Key,
                    rgba!,
                    atlasW,
                    atlasH,
                    drawW,
                    drawH,
                    adv,
                    cx,
                    cyW,
                    msdfRange));
            }
            catch
            {
                _inFlightJobs.TryRemove(job.Key, out _);
            }
        }
    }

    private bool TryRasterizeAndUploadSynchronously(
        IRenderer renderer,
        FontLibrary fonts,
        in TextStyle style,
        ReadOnlySpan<char> utf16Glyph,
        (string FamilyId, FontFaceKind Face, int SizeQuant, int Codepoint, int RasterRevision) key,
        out CachedGlyph glyph)
    {
        glyph = default;
        byte[]? rgba;
        int atlasW, atlasH;
        float drawW, drawH, adv, cx, cyW, msdfRange;
        var rasterStart = Stopwatch.GetTimestamp();
        lock (fonts.FontRasterSync)
        {
            _ = fonts.TryCreateFontUnlocked(in style, out var font, out _);
            _ = GlyphRasterizer.TryCreateGlyphMsdf(
                font,
                utf16Glyph,
                out rgba,
                out atlasW,
                out atlasH,
                out drawW,
                out drawH,
                out adv,
                out cx,
                out cyW,
                out msdfRange);
        }
        Interlocked.Add(ref s_rasterizeTicks, Stopwatch.GetTimestamp() - rasterStart);

        lock (_lock)
        {
            if (_glyphs.TryGetValue(key, out var existingAfterRaster))
            {
                glyph = existingAfterRaster;
                return true;
            }

            if (!TryPackAndUpload(renderer, rgba!, atlasW, atlasH, out var texId, out var uv))
                return false;

            var built = new CachedGlyph
            {
                TextureId = texId,
                WidthPx = drawW,
                HeightPx = drawH,
                OffsetPenToCenterX = cx,
                OffsetPenToCenterYWorld = cyW,
                AdvancePx = adv,
                UvRect = uv,
                MsdfPixelRange = msdfRange
            };
            _glyphs[key] = built;
            Interlocked.Increment(ref s_contentVersion);
            glyph = built;
            return true;
        }
    }
}
