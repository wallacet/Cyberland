using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Localization;

/// <summary>
/// Single entry point for localized strings and media: applies the active primary culture, English fallback,
/// and <c>Locale/&lt;culture&gt;/…</c> layout on the layered VFS. Gameplay code should use this instead of hardcoding
/// <c>Locale/en/…</c> or opening binary paths directly.
/// </summary>
/// <remarks>
/// All file IO goes through the same layered <see cref="Cyberland.Engine.Assets.VirtualFileSystem"/> passed when this façade was constructed
/// (for a running game, that is the same instance as <see cref="Cyberland.Engine.Modding.ModLoadContext.VirtualFileSystem"/>).
/// </remarks>
public interface ILocalizedContent
{
    /// <summary>Primary UI culture (e.g. <c>de-DE</c>); normalized, never empty.</summary>
    string PrimaryCultureName { get; }

    /// <summary>Shared string table; keys merged via <see cref="MergeStringTableAsync"/>.</summary>
    LocalizationManager Strings { get; }

    /// <summary>
    /// Merges <c>Locale/&lt;culture&gt;/&lt;tableFileName&gt;</c> for each culture in merge order (English base, then overlays).
    /// Later merges override keys from earlier files.
    /// </summary>
    Task MergeStringTableAsync(string tableFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous <see cref="MergeStringTableAsync"/> for contexts that are not async (for example
    /// <c>IMod.OnLoad</c>); reads each locale file with blocking IO.
    /// </summary>
    void MergeStringTable(string tableFileName);

    /// <summary>
    /// Resolves a canonical content path (e.g. <c>Textures/Hud/icon.png</c>) to the first existing VFS path among
    /// <c>Locale/&lt;culture&gt;/…</c> (most specific first) and the non-localized <paramref name="canonicalContentPath"/>.
    /// </summary>
    /// <returns>Normalized virtual path, or <c>null</c> if nothing exists.</returns>
    string? TryResolveLocalizedPath(string canonicalContentPath);

    /// <summary>Loads bytes for the resolved localized path, or <c>null</c> if missing.</summary>
    Task<byte[]?> TryLoadLocalizedBytesAsync(string canonicalContentPath,
        CancellationToken cancellationToken = default);

    /// <summary>Loads a texture for the resolved localized path, or <see cref="TextureId.MaxValue"/> if missing.</summary>
    Task<TextureId> TryLoadLocalizedTextureAsync(string canonicalContentPath,
        IRenderer renderer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a texture for the resolved localized path on the <strong>calling</strong> thread. Use this when registering into
    /// <see cref="IRenderer"/> from the host render / window thread; the async path may otherwise continue on a background thread.
    /// </summary>
    /// <returns>
    /// <see cref="TextureId.MaxValue"/> if no file exists. Otherwise a registered id from
    /// <see cref="IRenderer.RegisterTextureRgba"/> (or <see cref="TextureId.MaxValue"/> on decode/upload failure, matching async behavior).
    /// </returns>
    TextureId TryLoadLocalizedTexture(string canonicalContentPath, IRenderer renderer);

    /// <summary>Opens a stream for the resolved localized path, or <c>null</c> if missing.</summary>
    Stream? TryOpenLocalizedRead(string canonicalContentPath);
}
