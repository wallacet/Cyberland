namespace Cyberland.Engine.Modding;

/// <summary>
/// Describes a mod on disk. The loader mounts each mod's content in order, applies optional blocklists, then loads assemblies.
/// </summary>
public sealed class ModManifest
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "0.0.0";
    public string? EntryAssembly { get; init; }
    public string ContentRoot { get; init; } = "Content";
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
