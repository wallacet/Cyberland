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

    /// <summary>
    /// Blocks a relative path so it does not resolve even if an earlier mod provided it (see <see cref="VirtualFileSystem.BlockPath"/>).
    /// </summary>
    public void HideContentPath(string relativePath) =>
        VirtualFileSystem.BlockPath(relativePath);

    /// <summary>Registers or replaces a sequential ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="enabled">Initial enabled flag; use <see cref="SetSystemEnabled"/> to toggle later.</param>
    public void RegisterSequential(string logicalId, ISystem system, bool enabled = true) =>
        Scheduler.RegisterSequential(logicalId, system, enabled);

    /// <summary>Registers or replaces a parallel ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="enabled">Initial enabled flag; use <see cref="SetSystemEnabled"/> to toggle later.</param>
    public void RegisterParallel(string logicalId, IParallelSystem system, bool enabled = true) =>
        Scheduler.RegisterParallel(logicalId, system, enabled);

    /// <summary>Enables or disables a registered system without removing it (see <see cref="SystemScheduler.SetEnabled"/>).</summary>
    public bool SetSystemEnabled(string logicalId, bool enabled) => Scheduler.SetEnabled(logicalId, enabled);

    /// <summary>Removes a system registered under <paramref name="logicalId"/>.</summary>
    public bool TryUnregister(string logicalId) => Scheduler.TryUnregister(logicalId);

    /// <summary>Removes a localization key merged from an earlier mod (see <see cref="LocalizationManager.TryRemoveKey"/>).</summary>
    public bool TryRemoveLocalizationKey(string key) => Localization.TryRemoveKey(key);
}
