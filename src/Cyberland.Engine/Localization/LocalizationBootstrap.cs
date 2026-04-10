using Cyberland.Engine.Assets;

namespace Cyberland.Engine.Localization;

/// <summary>
/// Loads merged locale tables from the virtual file system (mods can override keys by load order).
/// </summary>
public static class LocalizationBootstrap
{
    /// <summary>
    /// Loads UTF-8 JSON (object of key → string) from <paramref name="relativePath"/> if it exists in the VFS and merges into <paramref name="localization"/>.
    /// </summary>
    public static async Task LoadAsync(LocalizationManager localization, AssetManager assets, string relativePath, CancellationToken cancellationToken = default)
    {
        if (!assets.FileSystem.Exists(relativePath))
            return;

        var bytes = await assets.LoadBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        localization.MergeJson(bytes);
    }
}
