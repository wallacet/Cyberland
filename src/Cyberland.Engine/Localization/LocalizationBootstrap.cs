using Cyberland.Engine.Assets;

namespace Cyberland.Engine.Localization;

/// <summary>
/// Loads merged locale tables from the virtual file system (mods can override keys by load order).
/// </summary>
public static class LocalizationBootstrap
{
    public static async Task LoadAsync(LocalizationManager localization, AssetManager assets, string relativePath, CancellationToken cancellationToken = default)
    {
        if (!assets.FileSystem.Exists(relativePath))
            return;

        var bytes = await assets.LoadBytesAsync(relativePath, cancellationToken).ConfigureAwait(false);
        localization.MergeJson(bytes);
    }
}
