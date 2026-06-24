using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Assets;
/// <summary>
/// Resolves UI and gameplay texture source strings (builtins, PNG paths, atlas#region) through localization.
/// </summary>
public sealed class TextureSourceResolver
{
    private readonly AssetManager _assets;
    private readonly Func<ILocalizedContent?> _getLocalized;
    private readonly Func<SpriteAtlasCatalog?> _getAtlasCatalog;

    /// <summary>Creates a resolver bound to VFS assets and optional localization/atlas services.</summary>
    public TextureSourceResolver(
        AssetManager assets,
        Func<ILocalizedContent?> getLocalized,
        Func<SpriteAtlasCatalog?> getAtlasCatalog)
    {
        _assets = assets;
        _getLocalized = getLocalized;
        _getAtlasCatalog = getAtlasCatalog;
    }

    /// <summary>Result of resolving a <c>sourceTexture</c> JSON field or similar spec string.</summary>
    public readonly record struct ResolvedTexture(
        TextureId TextureId,
        Vector4D<float> UvRect,
        NineSliceInsets NineSlice,
        int SourcePixelWidth,
        int SourcePixelHeight)
    {
        /// <summary>
        /// True when the resolve produced a drawable texture id (including the builtin <c>missing</c> alias).
        /// False only for empty or unparseable specs — not a signal that the asset exists on disk.
        /// </summary>
        public bool IsValid => TextureId != global::System.UInt32.MaxValue;
    }

    /// <summary>
    /// Parses <paramref name="sourceSpec"/>:
    /// <c>white</c>, <c>defaultNormal</c>, <c>missing</c>, <c>path.png</c>, or <c>manifest.json#region</c>.
    /// </summary>
    public ResolvedTexture Resolve(string? sourceSpec, IRenderer renderer)
    {
        if (string.IsNullOrWhiteSpace(sourceSpec))
            return new ResolvedTexture(TextureId.MaxValue, default, default, 0, 0);

        var spec = sourceSpec.Trim();
        if (spec.Equals("white", StringComparison.OrdinalIgnoreCase))
            return new ResolvedTexture(renderer.WhiteTextureId, FullUv, default, 1, 1);
        if (spec.Equals("defaultNormal", StringComparison.OrdinalIgnoreCase))
            return new ResolvedTexture(renderer.DefaultNormalTextureId, FullUv, default, 1, 1);
        if (spec.Equals("missing", StringComparison.OrdinalIgnoreCase))
            return new ResolvedTexture(renderer.MissingTextureId, FullUv, default, BuiltinTextures.MissingTextureSize, BuiltinTextures.MissingTextureSize);

        var hash = spec.IndexOf('#');
        if (hash >= 0)
            return ResolveAtlasRegion(spec[..hash].Trim(), spec[(hash + 1)..].Trim(), renderer);

        var localized = _getLocalized();
        if (localized is not null)
            return FromLoadResult(localized.TryLoadTextureFromCanonical(Normalize(spec), renderer), spec);

        return FromLoadResult(_assets.TryLoadTexture(Normalize(spec), renderer), spec);
    }

    private ResolvedTexture ResolveAtlasRegion(string manifestPath, string regionName, IRenderer renderer)
    {
        var catalog = _getAtlasCatalog();
        if (catalog is null || string.IsNullOrWhiteSpace(regionName))
            return new ResolvedTexture(renderer.MissingTextureId, FullUv, default, BuiltinTextures.MissingTextureSize, BuiltinTextures.MissingTextureSize);

        var atlas = catalog.GetOrLoad(Normalize(manifestPath), renderer);
        if (!atlas.TryGetRegion(regionName, out var region))
            return new ResolvedTexture(renderer.MissingTextureId, FullUv, default, BuiltinTextures.MissingTextureSize, BuiltinTextures.MissingTextureSize);

        return new ResolvedTexture(
            region.PageTextureId,
            region.UvRect,
            region.NineSlice,
            region.PixelWidth,
            region.PixelHeight);
    }

    private ResolvedTexture FromLoadResult(TextureLoadResult result, string canonicalPath)
    {
        if (result.Status != TextureLoadStatus.Ok)
            return new ResolvedTexture(result.Id, FullUv, default, BuiltinTextures.MissingTextureSize, BuiltinTextures.MissingTextureSize);
        var path = _getLocalized()?.TryResolveLocalizedPath(Normalize(canonicalPath)) ?? Normalize(canonicalPath);
        if (TryGetImageDimensions(path, out var w, out var h))
            return new ResolvedTexture(result.Id, FullUv, default, w, h);
        return new ResolvedTexture(result.Id, FullUv, default, 1, 1);
    }

    private bool TryGetImageDimensions(string path, out int width, out int height) =>
        TextureImageProbe.TryGetImageDimensions(_assets.FileSystem, path, out width, out height);

    private static Vector4D<float> FullUv => new(0f, 0f, 1f, 1f);

    private static string Normalize(string path) =>
        path.Trim().Replace('\\', '/').TrimStart('/');
}
