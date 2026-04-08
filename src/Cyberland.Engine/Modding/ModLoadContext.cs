using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Services exposed while a mod loads: register gameplay without reaching into host internals.
/// </summary>
public sealed class ModLoadContext
{
    public ModLoadContext(
        ModManifest manifest,
        string modDirectory,
        VirtualFileSystem vfs,
        LocalizationManager localization,
        World world,
        SystemScheduler scheduler,
        GameHostServices host)
    {
        Manifest = manifest;
        ModDirectory = modDirectory;
        VirtualFileSystem = vfs;
        Localization = localization;
        World = world;
        Scheduler = scheduler;
        Host = host;
    }

    public ModManifest Manifest { get; }
    public string ModDirectory { get; }
    public VirtualFileSystem VirtualFileSystem { get; }
    public LocalizationManager Localization { get; }
    public World World { get; }
    public SystemScheduler Scheduler { get; }

    /// <summary>Window input, key bindings, and renderer — populated by the host before mods load.</summary>
    public GameHostServices Host { get; }

    /// <summary>Mounts <see cref="ModManifest.ContentRoot"/> under this mod's folder.</summary>
    public void MountDefaultContent() =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, Manifest.ContentRoot));

    public void MountContentSubfolder(string relativeToModFolder) =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, relativeToModFolder));
}
