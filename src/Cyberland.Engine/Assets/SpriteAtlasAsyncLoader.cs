using System.Collections.Concurrent;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Prefetches sprite atlas manifest/page bytes on thread-pool workers; GPU upload runs on the render thread via <see cref="DrainPendingUploads"/>.
/// </summary>
public sealed class SpriteAtlasAsyncLoader
{
    private readonly AssetManager _assets;
    private readonly Func<ILocalizedContent?> _getLocalized;
    private readonly ConcurrentQueue<PendingAtlasLoad> _pending = new();

    /// <summary>Creates an async loader bound to VFS assets and localization.</summary>
    public SpriteAtlasAsyncLoader(AssetManager assets, Func<ILocalizedContent?> getLocalized)
    {
        _assets = assets;
        _getLocalized = getLocalized;
    }

    /// <summary>Pending loads not yet uploaded on the render thread.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Starts async decode for one atlas. Completes when <see cref="DrainPendingUploads"/> runs on the render thread.
    /// </summary>
    public Task<SpriteAtlas> LoadAsync(
        string canonicalManifestPath,
        IRenderer renderer,
        SpriteAtlasCatalog catalog,
        bool localeInvariant = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalManifestPath);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(catalog);

        var tcs = new TaskCompletionSource<SpriteAtlas>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var localized = _getLocalized();
                var norm = canonicalManifestPath.Trim().Replace('\\', '/').TrimStart('/');
                var resolved = localeInvariant
                    ? (_assets.FileSystem.Exists(norm) ? norm : null)
                    : localized?.TryResolveAtlasManifestPath(norm, false)
                      ?? (_assets.FileSystem.Exists(norm) ? norm : null);
                if (resolved is null)
                {
                    _pending.Enqueue(new PendingAtlasLoad(canonicalManifestPath, localeInvariant, tcs));
                    return;
                }

                var manifestBytes = _assets.LoadBytes(resolved);
                var dto = SpriteAtlasManifestParser.Parse(manifestBytes);
                foreach (var page in dto.Pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var pagePath = page.Path.Trim().Replace('\\', '/').TrimStart('/');
                    var resolvedPage = localeInvariant
                        ? (_assets.FileSystem.Exists(pagePath) ? pagePath : null)
                        : localized?.TryResolveLocalizedPath(pagePath)
                          ?? (_assets.FileSystem.Exists(pagePath) ? pagePath : null);
                    if (resolvedPage is null)
                        continue;
                    var bytes = _assets.LoadBytes(resolvedPage);
                    using var image = Image.Load<Rgba32>(bytes);
                }

                _pending.Enqueue(new PendingAtlasLoad(canonicalManifestPath, localeInvariant, tcs));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, cancellationToken);

        return tcs.Task;
    }

    /// <summary>Uploads atlases on the render thread and completes pending tasks.</summary>
    public void DrainPendingUploads(IRenderer renderer, SpriteAtlasCatalog catalog)
    {
        while (_pending.TryDequeue(out var pending))
        {
            try
            {
                var atlas = catalog.GetOrLoad(pending.CanonicalManifestPath, renderer, pending.LocaleInvariant);
                pending.Completion.TrySetResult(atlas);
            }
            catch (Exception ex)
            {
                pending.Completion.TrySetException(ex);
            }
        }
    }

    private sealed record PendingAtlasLoad(
        string CanonicalManifestPath,
        bool LocaleInvariant,
        TaskCompletionSource<SpriteAtlas> Completion);
}
