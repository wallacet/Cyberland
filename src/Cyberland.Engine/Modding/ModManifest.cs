namespace Cyberland.Engine.Modding;

/// <summary>
/// Describes a mod on disk. The loader mounts each mod's content in order, applies optional blocklists, then loads assemblies.
/// </summary>
/// <remarks>
/// <b>Content-only packs:</b> omit <see cref="EntryAssembly"/> (or leave it empty) for texture/audio-only overrides — no <c>IMod</c> assembly is loaded.
/// <b>Code-only mods:</b> you may keep the default <see cref="ContentRoot"/> of <c>Content</c> even when that folder is absent; the VFS mount is skipped when the path does not exist.
/// Avoid setting <see cref="ContentRoot"/> to an empty string: that can mount the entire mod directory into the VFS.
/// </remarks>
public sealed class ModManifest
{
    /// <summary>Stable id used for exclusions (<c>--exclude-mods</c>) and ordering; should be unique.</summary>
    public string Id { get; init; } = "";
    /// <summary>Human-readable title (UI / logs).</summary>
    public string Name { get; init; } = "";
    /// <summary>Semantic or marketing version string.</summary>
    public string Version { get; init; } = "0.0.0";
    /// <summary>Filename of the mod DLL inside the mod folder (e.g. <c>MyMod.dll</c>); omit for content-only packs.</summary>
    public string? EntryAssembly { get; init; }
    /// <summary>Subfolder under the mod directory to mount into the VFS first (default <c>Content</c>).</summary>
    public string ContentRoot { get; init; } = "Content";
    /// <summary>Lower runs first when sorting; ties broken by <see cref="Id"/>.</summary>
    public int LoadOrder { get; init; }

    /// <summary>
    /// When true, the loader skips this mod entirely: no content mount, no blocklist, no assembly load.
    /// </summary>
    public bool Disabled { get; init; }

    /// <summary>
    /// Relative paths (virtual FS) hidden after this mod's content is mounted; blocks win over all mounts.
    /// </summary>
    public string[]? ContentBlocklist { get; init; }
}
