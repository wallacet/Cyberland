using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Loads pre-baked MSDF atlas manifests and seeds <see cref="TextGlyphCache"/> before runtime fallback rasterization.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two load paths:</b> <see cref="LoadFromPath"/> (synchronous decode + upload on the caller) vs <see cref="LoadFromPathAsync"/> (decode on a thread-pool worker,
/// then enqueue for <see cref="DrainPendingUploads"/> on the render thread). The async task does not complete until <see cref="DrainPendingUploads"/> runs.
/// </para>
/// <para>
/// <b>Mod startup:</b> <see cref="Modding.ModLoader.LoadAll"/> awaits <see cref="Modding.IMod.OnLoadAsync"/> before the first frame. Mod code must not
/// <c>await</c> <see cref="LoadFromPathAsync"/> from <c>OnLoadAsync</c> — use <see cref="Modding.ModLoadContext.LoadBakedMsdfAtlas"/> or fire-and-forget
/// <see cref="Modding.ModLoadContext.LoadBakedMsdfAtlasAsync"/>.
/// </para>
/// </remarks>
internal sealed class BakedMsdfAtlasLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public sealed record LoadResult(
        string ManifestPath,
        bool Loaded,
        string Message,
        int GlyphCount,
        int PageCount);

    private sealed record DecodedPage(string Path, int Width, int Height, byte[] Rgba);
    private sealed record DecodedAtlas(
        string SourceLabel,
        BakedMsdfAtlasManifest Manifest,
        DecodedPage[] Pages,
        TextGlyphCache Cache,
        TaskCompletionSource<LoadResult>? Completion);

    private readonly ConcurrentQueue<DecodedAtlas> _pendingDecodedAtlases = new();

    /// <summary>
    /// Loads and uploads one atlas in a single call on the <strong>current thread</strong> (manifest + page decode, then GPU upload).
    /// </summary>
    /// <remarks>
    /// Use from mod <see cref="Modding.IMod.OnLoadAsync"/> when you must block until glyphs exist and the host allows synchronous <see cref="IRenderer"/> uploads on the load thread.
    /// </remarks>
    public LoadResult LoadFromPath(
        AssetManager assets,
        IRenderer renderer,
        TextGlyphCache cache,
        string manifestPath,
        int pageBudget = int.MaxValue)
    {
        if (!TryResolveManifest(assets, manifestPath, out var manifest, out var readPageBytes, out var sourceLabel, out var reason))
            return new LoadResult(manifestPath, false, reason, 0, 0);

        var limited = LimitManifestPages(manifest, pageBudget);
        if (!TryDecodePages(sourceLabel, limited, readPageBytes, out var decodedPages, out reason))
            return new LoadResult(sourceLabel, false, reason, 0, 0);

        return UploadDecoded(sourceLabel, limited, decodedPages, renderer, cache);
    }

    // Backward-compatible alias while call sites migrate to the unified name.
    public LoadResult LoadFromVfs(
        AssetManager assets,
        IRenderer renderer,
        TextGlyphCache cache,
        string manifestPath) =>
        LoadFromPath(assets, renderer, cache, manifestPath);

    // Backward-compatible helper for tests and direct resource fixtures.
    public LoadResult LoadFromResource(
        string logicalName,
        BakedMsdfAtlasManifest manifest,
        Func<string, byte[]> readPageBytes,
        IRenderer renderer,
        TextGlyphCache cache)
    {
        if (!TryDecodePages(logicalName, manifest, readPageBytes, out var decodedPages, out var reason))
            return new LoadResult(logicalName, false, reason, 0, 0);
        return UploadDecoded(logicalName, manifest, decodedPages, renderer, cache);
    }

    /// <summary>
    /// Decodes atlas pages on a thread-pool worker, then enqueues GPU upload for <see cref="DrainPendingUploads"/>.
    /// </summary>
    /// <remarks>
    /// The returned task completes only after <see cref="DrainPendingUploads"/> has dequeued and uploaded this atlas.
    /// Do not <c>await</c> from <see cref="Modding.IMod.OnLoadAsync"/> while <see cref="Modding.ModLoader.LoadAll"/> still blocks the startup thread (deadlock vs first render drain).
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Background decode queue behavior is integration-tested through host render drains.")]
    public Task<LoadResult> LoadFromPathAsync(
        AssetManager assets,
        TextGlyphCache cache,
        string manifestPath,
        CancellationToken cancellationToken = default,
        int pageBudget = int.MaxValue)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<LoadResult>(cancellationToken);

        var completion = new TaskCompletionSource<LoadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(() =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }

                if (!TryResolveManifest(assets, manifestPath, out var manifest, out var readPageBytes, out var sourceLabel, out var reason))
                {
                    completion.TrySetResult(new LoadResult(manifestPath, false, reason, 0, 0));
                    return;
                }

                var limited = LimitManifestPages(manifest, pageBudget);
                if (!TryDecodePages(sourceLabel, limited, readPageBytes, out var decodedPages, out reason))
                {
                    completion.TrySetResult(new LoadResult(sourceLabel, false, reason, 0, 0));
                    return;
                }

                _pendingDecodedAtlases.Enqueue(new DecodedAtlas(sourceLabel, limited, decodedPages, cache, completion));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetResult(new LoadResult(manifestPath, false, ex.Message, 0, 0));
            }
        });

        return completion.Task;
    }

    /// <summary>
    /// Uploads all atlases queued by <see cref="LoadFromPathAsync"/> and completes their tasks (call from the render / presentation thread).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Background queue drain and callback interleaving are covered by integration runs.")]
    public int DrainPendingUploads(
        IRenderer renderer,
        Action<LoadResult>? onProcessed = null)
    {
        var imported = 0;
        while (_pendingDecodedAtlases.TryDequeue(out var pending))
        {
            LoadResult result;
            try
            {
                result = UploadDecoded(pending.SourceLabel, pending.Manifest, pending.Pages, renderer, pending.Cache);
            }
            catch (Exception ex)
            {
                result = new LoadResult(pending.SourceLabel, false, ex.Message, 0, 0);
            }

            if (result.Loaded)
                imported += result.GlyphCount;
            pending.Completion?.TrySetResult(result);
            onProcessed?.Invoke(result);
        }

        return imported;
    }

    [ExcludeFromCodeCoverage(Justification = "Resolver fallback branches depend on filesystem and embedded resource faults.")]
    private static bool TryResolveManifest(
        AssetManager assets,
        string manifestPath,
        out BakedMsdfAtlasManifest manifest,
        out Func<string, byte[]> readPageBytes,
        out string sourceLabel,
        out string reason)
    {
        manifest = null!;
        readPageBytes = null!;
        sourceLabel = manifestPath;
        reason = "unknown";

        try
        {
            manifest = assets.LoadJsonAsync<BakedMsdfAtlasManifest>(manifestPath, JsonOptions).GetAwaiter().GetResult();
            var manifestDir = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
            // MsdfAtlasBaker writes pages as "{atlasStem}.pageN.png" next to "{atlasStem}.manifest.json" while the JSON
            // still lists "pageN.png". Prefix bare page*.png so VFS loads match on-disk layout (mods + copied engine bakes).
            var atlasStem = TryGetBakedAtlasStemFromManifestVirtualPath(manifestPath);
            readPageBytes = relPath =>
            {
                var page = relPath.Replace('\\', '/');
                if (atlasStem is not null
                    && page.Length > 0
                    && page.IndexOf('/') < 0
                    && page.StartsWith("page", StringComparison.Ordinal)
                    && page.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    page = $"{atlasStem}.{page}";
                return assets.LoadBytes(JoinManifestPath(manifestDir, page));
            };
            sourceLabel = manifestPath;
            return true;
        }
        catch (Exception vfsEx)
        {
            if (BuiltinFonts.TryResolveBakedAtlasFromVirtualPath(manifestPath, out manifest, out readPageBytes))
            {
                sourceLabel = manifestPath;
                return true;
            }

            reason = $"manifest not found or invalid ('{manifestPath}'): {vfsEx.Message}";
            return false;
        }
    }

    [ExcludeFromCodeCoverage(Justification = "Image decode failures are environment/toolchain dependent; success path is unit-tested.")]
    private static bool TryDecodePages(
        string sourceLabel,
        BakedMsdfAtlasManifest manifest,
        Func<string, byte[]> readPageBytes,
        out DecodedPage[] pages,
        out string reason)
    {
        pages = Array.Empty<DecodedPage>();
        reason = "ok";
        var decoded = new DecodedPage[manifest.Pages.Length];
        for (var i = 0; i < manifest.Pages.Length; i++)
        {
            try
            {
                var raw = readPageBytes(manifest.Pages[i].Path);
                using var image = Image.Load<Rgba32>(raw);
                var rgba = new byte[image.Width * image.Height * 4];
                image.CopyPixelDataTo(rgba);
                decoded[i] = new DecodedPage(manifest.Pages[i].Path, image.Width, image.Height, rgba);
            }
            catch (Exception ex)
            {
                reason = $"failed to decode page '{manifest.Pages[i].Path}' for '{sourceLabel}': {ex.Message}";
                return false;
            }
        }

        pages = decoded;
        return true;
    }

    private static LoadResult UploadDecoded(
        string sourceLabel,
        BakedMsdfAtlasManifest manifest,
        DecodedPage[] decodedPages,
        IRenderer renderer,
        TextGlyphCache cache)
    {
        if (manifest.RasterRevision != GlyphRasterizer.RasterRevision)
        {
            return new LoadResult(
                sourceLabel,
                false,
                $"raster revision mismatch (manifest={manifest.RasterRevision}, engine={GlyphRasterizer.RasterRevision})",
                0,
                0);
        }

        if (!TryParseFace(manifest.Face, out var face))
            return new LoadResult(sourceLabel, false, $"unknown face '{manifest.Face}'", 0, 0);

        var pageTextures = new TextureId[decodedPages.Length];
        for (var i = 0; i < decodedPages.Length; i++)
        {
            var page = decodedPages[i];
            var tex = renderer.RegisterTextureRgbaLinear(page.Rgba, page.Width, page.Height);
            if (tex == TextureId.MaxValue)
            {
                return new LoadResult(
                    sourceLabel,
                    false,
                    $"failed to upload page '{page.Path}'",
                    0,
                    i);
            }
            pageTextures[i] = tex;
        }

        var sizeQuant = FontLibrary.QuantizeEmSizePixels(manifest.SizePixels);
        var imported = 0;
        var invW = 1f / manifest.PageSizePixels;
        var invH = 1f / manifest.PageSizePixels;
        foreach (var glyph in manifest.Glyphs)
        {
            if ((uint)glyph.PageIndex >= pageTextures.Length)
                continue;
            glyph.UvRect = new Vector4D<float>(
                glyph.X * invW,
                glyph.Y * invH,
                (glyph.X + glyph.Width) * invW,
                (glyph.Y + glyph.Height) * invH);
            cache.RegisterBakedGlyph(
                manifest.FamilyId,
                face,
                sizeQuant,
                glyph.CodePoint,
                manifest.RasterRevision,
                new TextGlyphCache.CachedGlyph
                {
                    TextureId = pageTextures[glyph.PageIndex],
                    WidthPx = glyph.DrawWidthPx,
                    HeightPx = glyph.DrawHeightPx,
                    OffsetPenToCenterX = glyph.OffsetPenToCenterX,
                    OffsetPenToCenterYWorld = glyph.OffsetPenToCenterYWorld,
                    AdvancePx = glyph.AdvancePx,
                    UvRect = glyph.UvRect,
                    MsdfPixelRange = glyph.MsdfPixelRange
                });
            imported++;
        }

        return new LoadResult(sourceLabel, true, "ok", imported, pageTextures.Length);
    }

    [ExcludeFromCodeCoverage(Justification = "Page budget edge branches are exercised in startup integration paths.")]
    private static BakedMsdfAtlasManifest LimitManifestPages(BakedMsdfAtlasManifest source, int pageBudget)
    {
        if (pageBudget <= 0)
        {
            return new BakedMsdfAtlasManifest
            {
                Version = source.Version,
                FamilyId = source.FamilyId,
                Face = source.Face,
                SizePixels = source.SizePixels,
                RasterRevision = source.RasterRevision,
                PageSizePixels = source.PageSizePixels,
                Pages = Array.Empty<BakedMsdfAtlasPageRef>(),
                Glyphs = Array.Empty<BakedMsdfGlyphEntry>()
            };
        }

        if (source.Pages.Length <= pageBudget)
            return source;

        var keptPages = source.Pages.AsSpan(0, pageBudget).ToArray();
        var keptGlyphs = new List<BakedMsdfGlyphEntry>(source.Glyphs.Length);
        foreach (var glyph in source.Glyphs)
        {
            if (glyph.PageIndex < pageBudget)
                keptGlyphs.Add(glyph);
        }

        return new BakedMsdfAtlasManifest
        {
            Version = source.Version,
            FamilyId = source.FamilyId,
            Face = source.Face,
            SizePixels = source.SizePixels,
            RasterRevision = source.RasterRevision,
            PageSizePixels = source.PageSizePixels,
            Pages = keptPages,
            Glyphs = keptGlyphs.ToArray()
        };
    }

    private static string JoinManifestPath(string? manifestDir, string relPath)
    {
        if (string.IsNullOrWhiteSpace(manifestDir))
            return relPath.Replace('\\', '/');
        return $"{manifestDir}/{relPath.Replace('\\', '/')}";
    }

    /// <summary>
    /// When the manifest virtual path ends with <c>.manifest.json</c>, returns the filename stem (atlas id) used by the baker
    /// for sibling page PNGs; otherwise <see langword="null"/> so legacy manifests keep literal page paths.
    /// </summary>
    private static string? TryGetBakedAtlasStemFromManifestVirtualPath(string manifestPath)
    {
        var norm = manifestPath.Replace('\\', '/');
        var slash = norm.LastIndexOf('/');
        var file = slash >= 0 ? norm[(slash + 1)..] : norm;
        const string suffix = ".manifest.json";
        if (!file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;
        return file[..^suffix.Length];
    }

    private static bool TryParseFace(string face, out FontFaceKind kind)
    {
        switch (face)
        {
            case "Regular":
                kind = FontFaceKind.Regular;
                return true;
            case "Bold":
                kind = FontFaceKind.Bold;
                return true;
            case "Italic":
                kind = FontFaceKind.Italic;
                return true;
            case "BoldItalic":
                kind = FontFaceKind.BoldItalic;
                return true;
            default:
                kind = FontFaceKind.Regular;
                return false;
        }
    }
}
