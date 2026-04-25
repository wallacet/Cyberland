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

            // Same bytes as <see cref="MergeStringTableAsync"/>, but synchronous for <c>IMod.OnLoad</c> (no async hop).
            var bytes = _assets.LoadBytes(path);
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
    public Task<TextureId> TryLoadLocalizedTextureAsync(string canonicalContentPath,
        IRenderer renderer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Defer to synchronous registration so the caller’s thread (typically the window/render thread) runs
        // IRenderer.RegisterTextureRgba. Async IO + ConfigureAwait(false) previously risked pool-thread upload (unsafe for Vulkan).
        return Task.FromResult(TryLoadLocalizedTexture(canonicalContentPath, renderer));
    }

    /// <inheritdoc />
    public TextureId TryLoadLocalizedTexture(string canonicalContentPath, IRenderer renderer)
    {
        var path = TryResolveLocalizedPath(canonicalContentPath);
        if (path is null)
            return TextureId.MaxValue;

        try
        {
            return _assets.LoadTexture(path, renderer);
        }
        // Bad bytes at the resolved VFS path (e.g. corrupt download, LFS pointer, or wrong file) are treated like missing
        // so <see cref="Scene.SpriteLocalizedAsset.KeepExistingOnMissing"/> and callers can keep a safe fallback albedo.
        catch (Exception ex) when (ex is InvalidImageContentException or UnknownImageFormatException)
        {
            return TextureId.MaxValue;
        }
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
