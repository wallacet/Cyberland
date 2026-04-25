using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cyberland.Engine.Localization;

/// <inheritdoc />
public sealed class LocalizedContent : ILocalizedContent
{
    private readonly VirtualFileSystem _vfs;
    private readonly AssetManager _assets;
    private readonly string _primaryCulture;

    /// <summary>Creates the façade; <paramref name="localization"/> is the shared host/mod string table.</summary>
    public LocalizedContent(LocalizationManager localization, VirtualFileSystem vfs, string primaryCultureName)
    {
        Strings = localization;
        _vfs = vfs;
        _assets = new AssetManager(vfs);
        _primaryCulture = LocalizationCultureChains.NormalizeCultureName(primaryCultureName);
    }

    /// <inheritdoc />
    public LocalizationManager Strings { get; }

    /// <inheritdoc />
    public string PrimaryCultureName => _primaryCulture;

    /// <inheritdoc />
    public async Task MergeStringTableAsync(string tableFileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableFileName))
            return;

        var file = tableFileName.Trim().Replace('\\', '/').TrimStart('/');
        foreach (var culture in LocalizationCultureChains.StringTableMergeOrder(_primaryCulture))
        {
            var path = $"Locale/{culture}/{file}";
            if (!_assets.FileSystem.Exists(path))
                continue;

            var bytes = await _assets.LoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
            Strings.MergeJson(bytes);
        }
    }

    /// <inheritdoc />
    public void MergeStringTable(string tableFileName)
    {
        if (string.IsNullOrWhiteSpace(tableFileName))
            return;

        var file = tableFileName.Trim().Replace('\\', '/').TrimStart('/');
        foreach (var culture in LocalizationCultureChains.StringTableMergeOrder(_primaryCulture))
        {
            var path = $"Locale/{culture}/{file}";
            if (!_assets.FileSystem.Exists(path))
                continue;

            // Same IO as <see cref="MergeStringTableAsync"/>, but synchronous for <c>IMod.OnLoad</c> (blocking wait on the load thread).
            var bytes = _assets.LoadBytesAsync(path, default).GetAwaiter().GetResult();
            Strings.MergeJson(bytes);
        }
    }

    /// <inheritdoc />
    public string? TryResolveLocalizedPath(string canonicalContentPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalContentPath))
            return null;

        var norm = canonicalContentPath.Trim().Replace('\\', '/').TrimStart('/');
        foreach (var culture in LocalizationCultureChains.AssetResolutionCultureOrder(_primaryCulture))
        {
            var p = $"Locale/{culture}/{norm}";
            if (_vfs.Exists(p))
                return p;
        }

        return _vfs.Exists(norm) ? norm : null;
    }

    /// <inheritdoc />
    public async Task<byte[]?> TryLoadLocalizedBytesAsync(string canonicalContentPath,
        CancellationToken cancellationToken = default)
    {
        var path = TryResolveLocalizedPath(canonicalContentPath);
        if (path is null)
            return null;

        return await _assets.LoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TextureId> TryLoadLocalizedTextureAsync(string canonicalContentPath,
        IRenderer renderer,
        CancellationToken cancellationToken = default)
    {
        var path = TryResolveLocalizedPath(canonicalContentPath);
        if (path is null)
            return TextureId.MaxValue;

        return await _assets.LoadTextureAsync(path, renderer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Stream? TryOpenLocalizedRead(string canonicalContentPath)
    {
        var path = TryResolveLocalizedPath(canonicalContentPath);
        if (path is null)
            return null;

        return _assets.FileSystem.TryOpenRead(path, out var s) ? s : null;
    }
}
