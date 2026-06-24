using System.Collections.Concurrent;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Loads and caches culture-scoped sprite atlases (manifest JSON + PNG pages) through <see cref="ILocalizedContent"/>.
/// </summary>
/// <remarks>
/// <para>Cache deduplication is per <c>(culture, resolvedManifestPath, localeInvariant)</c> so language packs do not share GPU uploads with the base locale.</para>
/// <para><b>Thread-safety:</b> <see cref="ConcurrentDictionary{TKey,TValue}"/> backs the cache; concurrent <see cref="GetOrLoad"/> calls may race on first miss and upload duplicate pages — prefer synchronous preload from the main thread during mod load (<see cref="Modding.ModLoadContext.LoadSpriteAtlas"/>).</para>
/// </remarks>
public sealed class SpriteAtlasCatalog
{
    private readonly AssetManager _assets;
    private readonly Func<ILocalizedContent?> _getLocalized;
    private readonly ConcurrentDictionary<(string Culture, string ResolvedManifestPath, bool LocaleInvariant), SpriteAtlas> _cache = new();

    /// <summary>Creates a catalog reading through <paramref name="assets"/> and resolving locale via <paramref name="getLocalized"/>.</summary>
    public SpriteAtlasCatalog(AssetManager assets, Func<ILocalizedContent?> getLocalized)
    {
        _assets = assets;
        _getLocalized = getLocalized;
    }

    /// <summary>
    /// Returns a loaded atlas for the canonical manifest path, uploading pages on cache miss.
    /// </summary>
    public SpriteAtlas GetOrLoad(string canonicalManifestPath, IRenderer renderer, bool localeInvariant = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalManifestPath);
        ArgumentNullException.ThrowIfNull(renderer);

        if (TryGetCached(canonicalManifestPath, localeInvariant, out var cached))
            return cached;

        var localized = _getLocalized();
        var culture = localized?.PrimaryCultureName ?? LocalizationCultureChains.EnglishNeutral;
        var normCanonical = NormalizePath(canonicalManifestPath);

        var resolvedManifest = localeInvariant
            ? (_assets.FileSystem.Exists(normCanonical) ? normCanonical : null)
            : localized?.TryResolveAtlasManifestPath(normCanonical, localeInvariant: false)
              ?? (_assets.FileSystem.Exists(normCanonical) ? normCanonical : null);

        if (resolvedManifest is null)
            return CreateMissingAtlas(normCanonical, culture, renderer);

        var key = (culture, resolvedManifest, localeInvariant);
        return _cache.GetOrAdd(key, _ => LoadAtlas(normCanonical, resolvedManifest, culture, renderer, localized, localeInvariant));
    }

    /// <summary>
    /// Returns a previously loaded atlas without resolving manifests or uploading pages on cache miss.
    /// </summary>
    public bool TryGetCached(string canonicalManifestPath, bool localeInvariant, out SpriteAtlas atlas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalManifestPath);
        atlas = null!;

        if (!TryBuildCacheKey(canonicalManifestPath, localeInvariant, out var key))
            return false;

        if (!_cache.TryGetValue(key, out var cached))
            return false;

        atlas = cached;
        return true;
    }

    private bool TryBuildCacheKey(
        string canonicalManifestPath,
        bool localeInvariant,
        out (string Culture, string ResolvedManifestPath, bool LocaleInvariant) key)
    {
        key = default;
        var localized = _getLocalized();
        var culture = localized?.PrimaryCultureName ?? LocalizationCultureChains.EnglishNeutral;
        var normCanonical = NormalizePath(canonicalManifestPath);

        var resolvedManifest = localeInvariant
            ? (_assets.FileSystem.Exists(normCanonical) ? normCanonical : null)
            : localized?.TryResolveAtlasManifestPath(normCanonical, localeInvariant: false)
              ?? (_assets.FileSystem.Exists(normCanonical) ? normCanonical : null);

        if (resolvedManifest is null)
            return false;

        key = (culture, resolvedManifest, localeInvariant);
        return true;
    }

    /// <summary>Clears all cached atlases (dev hot-reload).</summary>
    public void ClearCache() => _cache.Clear();

    private SpriteAtlas LoadAtlas(
        string canonicalManifestPath,
        string resolvedManifestPath,
        string culture,
        IRenderer renderer,
        ILocalizedContent? localized,
        bool localeInvariant)
    {
        try
        {
            var bytes = _assets.LoadBytes(resolvedManifestPath);
            var dto = SpriteAtlasManifestParser.Parse(bytes);
            if (dto.SchemaVersion != 1)
                throw new InvalidOperationException($"Unsupported sprite atlas schema version {dto.SchemaVersion}.");

            var pageCount = dto.Pages.Length;
            var pageTextureIds = new TextureId[pageCount];
            var pageWidths = new int[pageCount];
            var pageHeights = new int[pageCount];

            for (var p = 0; p < pageCount; p++)
            {
                var pagePath = NormalizePath(dto.Pages[p].Path);
                var resolvedPage = localeInvariant
                    ? (_assets.FileSystem.Exists(pagePath) ? pagePath : null)
                    : localized?.TryResolveLocalizedPath(pagePath)
                      ?? (_assets.FileSystem.Exists(pagePath) ? pagePath : null);

                if (resolvedPage is null || !TryGetImageDimensions(resolvedPage, out pageWidths[p], out pageHeights[p]))
                {
                    pageTextureIds[p] = renderer.MissingTextureId;
                    pageWidths[p] = 1;
                    pageHeights[p] = 1;
                    TextureLoadDiagnostics.LogFailureOnce(resolvedPage ?? pagePath, TextureLoadStatus.NotFound);
                    continue;
                }

                var load = _assets.TryLoadTexture(resolvedPage, renderer);
                pageTextureIds[p] = load.Id;
            }

            var regions = new Dictionary<string, SpriteAtlasRegion>(StringComparer.Ordinal);
            foreach (var r in dto.Regions)
            {
                if (string.IsNullOrWhiteSpace(r.Name) || r.PixelRect.Length < 4)
                    continue;
                var pageIndex = r.PageIndex;
                if (pageIndex < 0 || pageIndex >= pageCount)
                    continue;

                var pw = pageWidths[pageIndex];
                var ph = pageHeights[pageIndex];
                var px = r.PixelRect[0];
                var py = r.PixelRect[1];
                var rw = r.PixelRect[2];
                var rh = r.PixelRect[3];
                var uv = SpriteAtlasManifestParser.PixelRectToUvRect(px, py, rw, rh, pw, ph);

                var pivot = new Vector2D<float>(0.5f, 1f);
                if (r.Pivot is { Length: >= 2 })
                    pivot = new Vector2D<float>(r.Pivot[0], r.Pivot[1]);

                var half = new Vector2D<float>(rw * 0.5f, rh * 0.5f);
                if (r.SizeWorld is { Length: >= 2 })
                    half = new Vector2D<float>(r.SizeWorld[0] * 0.5f, r.SizeWorld[1] * 0.5f);

                NineSliceInsets nineSlice = default;
                if (r.NineSlice is { Length: >= 4 })
                    nineSlice = new NineSliceInsets(r.NineSlice[0], r.NineSlice[1], r.NineSlice[2], r.NineSlice[3]);

                regions[r.Name] = new SpriteAtlasRegion
                {
                    Name = r.Name,
                    PageTextureId = pageTextureIds[pageIndex],
                    PageIndex = pageIndex,
                    PixelX = px,
                    PixelY = py,
                    PixelWidth = rw,
                    PixelHeight = rh,
                    UvRect = uv,
                    Pivot = pivot,
                    HalfExtentsWorld = half,
                    NineSlice = nineSlice
                };
            }

            var animations = new Dictionary<string, SpriteAtlasAnimationClip>(StringComparer.Ordinal);
            if (dto.Animations is not null)
            {
                foreach (var (name, anim) in dto.Animations)
                {
                    if (anim.RegionNames.Length == 0)
                        continue;
                    animations[name] = new SpriteAtlasAnimationClip
                    {
                        RegionNames = anim.RegionNames,
                        SecondsPerFrame = anim.SecondsPerFrame,
                        Loop = anim.Loop
                    };
                }
            }

            var sheets = new Dictionary<string, SpriteAtlasSheetClip>(StringComparer.Ordinal);
            if (dto.Sheets is not null)
            {
                foreach (var (name, sheet) in dto.Sheets)
                {
                    if (string.IsNullOrWhiteSpace(sheet.RegionName) || sheet.Columns <= 0 || sheet.FrameCount <= 0)
                        continue;
                    sheets[name] = new SpriteAtlasSheetClip
                    {
                        RegionName = sheet.RegionName,
                        Columns = sheet.Columns,
                        FrameCount = sheet.FrameCount,
                        SecondsPerFrame = sheet.SecondsPerFrame,
                        Loop = sheet.Loop
                    };
                }
            }

            return new SpriteAtlas(
                canonicalManifestPath,
                resolvedManifestPath,
                culture,
                pageWidths,
                pageHeights,
                regions,
                animations,
                sheets);
        }
        catch (Exception ex)
        {
            TextureLoadDiagnostics.LogFailureOnce(resolvedManifestPath, TextureLoadStatus.DecodeFailed);
            Console.Error.WriteLine($"[Cyberland.Engine] Sprite atlas manifest failed ({resolvedManifestPath}): {ex.Message}");
            return CreateMissingAtlas(canonicalManifestPath, culture, renderer);
        }
    }

    private static SpriteAtlas CreateMissingAtlas(string canonicalManifestPath, string culture, IRenderer renderer)
    {
        var missingRegion = new SpriteAtlasRegion
        {
            Name = "__missing__",
            PageTextureId = renderer.MissingTextureId,
            PageIndex = 0,
            PixelWidth = BuiltinTextures.MissingTextureSize,
            PixelHeight = BuiltinTextures.MissingTextureSize,
            UvRect = new Vector4D<float>(0f, 0f, 1f, 1f),
            HalfExtentsWorld = new Vector2D<float>(32f, 32f)
        };
        return new SpriteAtlas(
            canonicalManifestPath,
            canonicalManifestPath,
            culture,
            [BuiltinTextures.MissingTextureSize],
            [BuiltinTextures.MissingTextureSize],
            new Dictionary<string, SpriteAtlasRegion>(StringComparer.Ordinal) { [missingRegion.Name] = missingRegion },
            new Dictionary<string, SpriteAtlasAnimationClip>(StringComparer.Ordinal),
            new Dictionary<string, SpriteAtlasSheetClip>(StringComparer.Ordinal));
    }

    private bool TryGetImageDimensions(string path, out int width, out int height) =>
        TextureImageProbe.TryGetImageDimensions(_assets.FileSystem, path, out width, out height);

    private static string NormalizePath(string path) =>
        path.Trim().Replace('\\', '/').TrimStart('/');
}
