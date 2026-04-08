namespace Cyberland.Engine.Modding;

/// <summary>
/// Describes a mod on disk. The host loads manifests first, then assemblies, then mounts content.
/// </summary>
public sealed class ModManifest
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "0.0.0";
    public string? EntryAssembly { get; init; }
    public string ContentRoot { get; init; } = "Content";
    public int LoadOrder { get; init; }
}
