using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Everything your <see cref="IMod.OnLoad"/> needs to plug into the running game: ECS world, system scheduler, virtual files, locale, and host devices.
/// </summary>
/// <remarks>
/// Prefer registering work through this type instead of static globals so load order and tests stay predictable.
/// </remarks>
public sealed class ModLoadContext
{
    /// <summary>Constructs a context (normally only the <see cref="ModLoader"/> calls this).</summary>
    /// <param name="manifest">This mod’s parsed <c>manifest.json</c>.</param>
    /// <param name="modDirectory">Absolute path to this mod’s folder.</param>
    /// <param name="vfs">Layered virtual file system (may already include earlier mods).</param>
    /// <param name="localization">Merged localization tables.</param>
    /// <param name="world">Shared ECS world.</param>
    /// <param name="scheduler">Where this mod registers systems.</param>
    /// <param name="host">Renderer, input, and optional stores from the host.</param>
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

    /// <summary>Metadata deserialized from this mod’s <c>manifest.json</c>.</summary>
    public ModManifest Manifest { get; }
    /// <summary>Absolute path to this mod’s root folder on disk.</summary>
    public string ModDirectory { get; }
    /// <summary>Layered virtual file system; earlier mods may already have mounted content.</summary>
    public VirtualFileSystem VirtualFileSystem { get; }
    /// <summary>String table merged across mods; later mods can override keys.</summary>
    public LocalizationManager Localization { get; }
    /// <summary>Shared ECS world for entities and components.</summary>
    public World World { get; }
    /// <summary>Register <see cref="Core.Ecs.ISystem"/> / <see cref="Core.Ecs.IParallelSystem"/> implementations here.</summary>
    public SystemScheduler Scheduler { get; }

    /// <summary>Renderer, input, optional tilemap/particle stores — assigned by the host before <see cref="IMod.OnLoad"/>.</summary>
    public GameHostServices Host { get; }

    /// <summary>Mounts <see cref="ModManifest.ContentRoot"/> under this mod's folder.</summary>
    public void MountDefaultContent() =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, Manifest.ContentRoot));

    /// <summary>Mounts an extra folder under your mod directory (e.g. <c>"Optional/ExtraAssets"</c>).</summary>
    /// <param name="relativeToModFolder">Path relative to <see cref="ModDirectory"/>.</param>
    public void MountContentSubfolder(string relativeToModFolder) =>
        VirtualFileSystem.Mount(Path.Combine(ModDirectory, relativeToModFolder));

    /// <summary>
    /// Blocks a relative path so it does not resolve even if an earlier mod provided it (see <see cref="VirtualFileSystem.BlockPath"/>).
    /// </summary>
    public void HideContentPath(string relativePath) =>
        VirtualFileSystem.BlockPath(relativePath);

    /// <summary>Registers or replaces a sequential ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="logicalId">Stable id for this system (enable/disable, diagnostics).</param>
    /// <param name="system">Sequential system instance.</param>
    /// <param name="enabled">Initial enabled flag; use <see cref="SetSystemEnabled"/> to toggle later.</param>
    public void RegisterSequential(string logicalId, ISystem system, bool enabled = true) =>
        Scheduler.RegisterSequential(logicalId, system, enabled);

    /// <summary>Registers or replaces a parallel ECS system under <paramref name="logicalId"/>.</summary>
    /// <param name="logicalId">Stable id for this system (enable/disable, diagnostics).</param>
    /// <param name="system">Parallel system instance.</param>
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
