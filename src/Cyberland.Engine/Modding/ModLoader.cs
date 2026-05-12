using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Discovers mod folders under a root, applies each enabled mod’s content to the VFS, then loads mod assemblies.
/// </summary>
/// <remarks>
/// <b>Load flow (two passes):</b> (1) Enumerate <c>Mods/*/manifest.json</c>, skip disabled/excluded ids, sort by
/// <see cref="ModManifest.LoadOrder"/> then id. For each manifest, record it and mount <see cref="ModManifest.ContentRoot"/>,
/// then apply <see cref="ModManifest.ContentBlocklist"/> via <see cref="VirtualFileSystem.BlockPath"/>. (2) For each entry
/// with an <see cref="ModManifest.EntryAssembly"/>, load the DLL from disk, resolve a concrete <see cref="IMod"/> (optional
/// <see cref="ModManifest.EntryType"/> hint, else scan <see cref="Assembly.GetExportedTypes"/>), construct it,
/// and call <see cref="IMod.OnLoadAsync"/> with a <see cref="ModLoadContext"/>.
/// <para>
/// Entry assemblies load in <see cref="AssemblyLoadContext.Default"/> so <see cref="IMod"/> matches the host’s
/// <c>Cyberland.Engine</c> contract. While an entry assembly is loading, <see cref="DefaultLoadContextResolving"/> uses
/// <see cref="SatelliteResolutionModDirectory"/> (thread-static) to resolve satellite DLLs from that mod’s folder (and optional
/// <c>lib\</c> subfolder) before other probing, excluding <c>Cyberland.Engine</c> so the contract stays unified with the host.
/// </para>
/// Mod loading is expected on the main thread during startup; <see cref="SatelliteResolutionModDirectory"/> is thread-static so nested loads do not cross mod directories.
/// Mods execute with host trust; third-party mods imply arbitrary native code (document for contributors, not end users).
/// </remarks>
public sealed class ModLoader
{
    [ExcludeFromCodeCoverage(Justification = "Timing entry DTO; phase totals are validated via ModLoader.LoadAll tests.")]
    internal sealed record ModLoadEntryTiming(
        string ModId,
        double AssemblyLoadMs,
        double TypeResolveMs,
        double OnLoadMs);

    [ExcludeFromCodeCoverage(Justification = "Timing aggregate DTO; phase totals are validated via ModLoader.LoadAll tests.")]
    internal sealed record ModLoadTiming(
        double EnumerateDirectoriesMs,
        double ParseManifestsMs,
        double SortManifestsMs,
        double MountContentMs,
        double LoadAssembliesAndModsMs,
        double TotalMs,
        int ManifestCount,
        int LoadedModCount,
        IReadOnlyList<ModLoadEntryTiming> Entries);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    static ModLoader()
    {
        AssemblyLoadContext.Default.Resolving += DefaultLoadContextResolving;
    }

    /// <summary>Manifests successfully staged in the last <see cref="LoadAll"/> (content pass + assembly load pass).</summary>
    public IReadOnlyList<ModManifest> LoadedManifests => _manifests;
    internal ModLoadTiming? LastLoadTiming { get; private set; }
    private readonly List<ModManifest> _manifests = new();
    private readonly List<IMod> _instances = new();

    /// <summary>While non-null, <see cref="DefaultLoadContextResolving"/> resolves satellites for this mod directory.</summary>
    [ThreadStatic]
    internal static string? SatelliteResolutionModDirectory;

    /// <summary>
    /// Resolves a dependency assembly from a mod directory tree (root and <c>lib\</c>), keeping <c>Cyberland.Engine</c> on the default context.
    /// Invoked from <see cref="AssemblyLoadContext.Default"/> while an entry assembly is loading (see <see cref="SatelliteResolutionModDirectory"/>).
    /// </summary>
    internal static Assembly? DefaultLoadContextResolving(AssemblyLoadContext? context, AssemblyName assemblyName)
    {
        _ = context;
        var dir = SatelliteResolutionModDirectory;
        return dir is null ? null : ResolveSatelliteAssembly(dir, assemblyName);
    }

    /// <summary>
    /// Resolves a dependency assembly from a mod directory tree (root and <c>lib\</c>), keeping <c>Cyberland.Engine</c> on the default context.
    /// Exposed for unit tests; production path is <see cref="DefaultLoadContextResolving"/>.
    /// </summary>
    internal static Assembly? ResolveSatelliteAssembly(string modDirectory, AssemblyName assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName.Name))
            return null;

        if (string.Equals(assemblyName.Name, "Cyberland.Engine", StringComparison.Ordinal))
            return null;

        var name = assemblyName.Name + ".dll";
        var path = Path.Combine(modDirectory, name);
        if (!File.Exists(path))
        {
            path = Path.Combine(modDirectory, "lib", name);
            if (!File.Exists(path))
                return null;
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }

    /// <summary>
    /// Mounts mod content in load order, applies blocklists, then loads each mod’s <see cref="ModManifest.EntryAssembly"/> and invokes <see cref="IMod.OnLoadAsync"/>.
    /// </summary>
    /// <param name="modsRootDirectory">Typically <c>Mods</c> next to the executable.</param>
    /// <param name="vfs">Shared layered file system.</param>
    /// <param name="localizedContent">Merged string tables + localized asset resolution.</param>
    /// <param name="world">Shared ECS world.</param>
    /// <param name="scheduler">Where mods register systems.</param>
    /// <param name="host">Renderer, input, optional stores.</param>
    /// <param name="excludedModIds">Mod ids to skip entirely (from <see cref="ExcludeModsParser"/>).</param>
    /// <remarks>
    /// Calling this again on the same <see cref="ModLoader"/> replaces the previous result: <see cref="UnloadAll"/> runs first
    /// (reverse <see cref="IMod.OnUnload"/>), then manifests and instances are repopulated. The host’s <see cref="VirtualFileSystem"/>
    /// is not cleared; mounts accumulate across repeated loads unless the host replaces or clears the <see cref="Assets.VirtualFileSystem"/>.
    /// <para>
    /// <see cref="IMod.OnLoadAsync"/> is completed synchronously via <c>GetAwaiter().GetResult()</c> on the load thread before the first render tick.
    /// Therefore <strong>do not await</strong> <see cref="ModLoadContext.LoadBakedMsdfAtlasAsync"/> inside <c>OnLoadAsync</c> — the task only completes after
    /// the render loop drains pending atlas uploads (deadlock). Use <c>_ = context.LoadBakedMsdfAtlasAsync(...)</c> or <see cref="ModLoadContext.LoadBakedMsdfAtlas"/>.
    /// </para>
    /// </remarks>
    public void LoadAll(
        string modsRootDirectory,
        VirtualFileSystem vfs,
        ILocalizedContent localizedContent,
        World world,
        SystemScheduler scheduler,
        GameHostServices host,
        IReadOnlySet<string>? excludedModIds = null)
    {
        var totalSw = Stopwatch.StartNew();
        LastLoadTiming = null;
        if (!Directory.Exists(modsRootDirectory))
        {
            LastLoadTiming = new ModLoadTiming(0d, 0d, 0d, 0d, 0d, totalSw.Elapsed.TotalMilliseconds, 0, 0, Array.Empty<ModLoadEntryTiming>());
            return;
        }

        UnloadAll();

        var enumSw = Stopwatch.StartNew();
        var dirs = Directory.GetDirectories(modsRootDirectory);
        enumSw.Stop();
        var manifests = new List<(string Dir, ModManifest M)>();
        var parseManifestSw = Stopwatch.StartNew();

        foreach (var dir in dirs)
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            var json = File.ReadAllText(manifestPath);
            var m = JsonSerializer.Deserialize<ModManifest>(json, JsonOptions);
            if (m is null || string.IsNullOrWhiteSpace(m.Id) || m.Disabled)
                continue;

            if (excludedModIds is not null && excludedModIds.Contains(m.Id))
                continue;

            manifests.Add((dir, m));
        }
        parseManifestSw.Stop();

        var sortSw = Stopwatch.StartNew();
        manifests.Sort(static (a, b) =>
        {
            var c = a.M.LoadOrder.CompareTo(b.M.LoadOrder);
            if (c != 0)
                return c;
            return string.CompareOrdinal(a.M.Id, b.M.Id);
        });
        sortSw.Stop();

        var mountSw = Stopwatch.StartNew();
        foreach (var entry in manifests)
        {
            _manifests.Add(entry.M);
            var contentPath = Path.Combine(entry.Dir, entry.M.ContentRoot);
            vfs.Mount(contentPath);
            if (entry.M.ContentBlocklist is { Length: > 0 })
            {
                foreach (var rel in entry.M.ContentBlocklist)
                    vfs.BlockPath(rel);
            }
        }
        mountSw.Stop();

        var loadSw = Stopwatch.StartNew();
        var entryTimings = new List<ModLoadEntryTiming>(manifests.Count);
        foreach (var entry in manifests)
        {
            if (string.IsNullOrWhiteSpace(entry.M.EntryAssembly))
                continue;

            var dll = Path.Combine(entry.Dir, entry.M.EntryAssembly);
            if (!File.Exists(dll))
                continue;

            var modDir = entry.Dir;
            SatelliteResolutionModDirectory = modDir;
            try
            {
                Assembly asm;
                var assemblySw = Stopwatch.StartNew();
                try
                {
                    asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
                }
                catch (BadImageFormatException)
                {
                    assemblySw.Stop();
                    continue;
                }
                assemblySw.Stop();

                Type? modType = null;
                var resolveTypeSw = Stopwatch.StartNew();
                if (!string.IsNullOrWhiteSpace(entry.M.EntryType))
                {
                    var hinted = asm.GetType(entry.M.EntryType!, throwOnError: false, ignoreCase: false);
                    if (hinted is not null && typeof(IMod).IsAssignableFrom(hinted) && hinted is { IsClass: true, IsAbstract: false })
                        modType = hinted;
                }

                if (modType is null)
                {
                    foreach (var t in asm.GetExportedTypes())
                    {
                        if (typeof(IMod).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
                        {
                            modType = t;
                            break;
                        }
                    }
                }
                resolveTypeSw.Stop();

                if (modType is null)
                    continue;

                var mod = (IMod)Activator.CreateInstance(modType)!;
                var ctx = new ModLoadContext(entry.M, entry.Dir, vfs, localizedContent, world, scheduler, host);
                var onLoadSw = Stopwatch.StartNew();
                mod.OnLoadAsync(ctx).GetAwaiter().GetResult();
                onLoadSw.Stop();
                _instances.Add(mod);
                entryTimings.Add(
                    new ModLoadEntryTiming(
                        entry.M.Id,
                        assemblySw.Elapsed.TotalMilliseconds,
                        resolveTypeSw.Elapsed.TotalMilliseconds,
                        onLoadSw.Elapsed.TotalMilliseconds));
            }
            finally
            {
                SatelliteResolutionModDirectory = null;
            }
        }
        loadSw.Stop();
        totalSw.Stop();
        LastLoadTiming = new ModLoadTiming(
            enumSw.Elapsed.TotalMilliseconds,
            parseManifestSw.Elapsed.TotalMilliseconds,
            sortSw.Elapsed.TotalMilliseconds,
            mountSw.Elapsed.TotalMilliseconds,
            loadSw.Elapsed.TotalMilliseconds,
            totalSw.Elapsed.TotalMilliseconds,
            manifests.Count,
            _instances.Count,
            entryTimings);
    }

    /// <summary>Calls <see cref="IMod.OnUnload"/> on loaded mods in reverse order and clears the manifest list.</summary>
    public void UnloadAll()
    {
        for (var i = _instances.Count - 1; i >= 0; i--)
            _instances[i].OnUnload();

        _instances.Clear();
        _manifests.Clear();
    }
}
