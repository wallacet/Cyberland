using System.Reflection;
using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Modding;

/// <summary>
/// Discovers mod folders, parses manifests, mounts content for every mod, then loads optional assemblies.
/// </summary>
public sealed class ModLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IReadOnlyList<ModManifest> LoadedManifests => _manifests;
    private readonly List<ModManifest> _manifests = new();
    private readonly List<IMod> _instances = new();

    public void LoadAll(
        string modsRootDirectory,
        VirtualFileSystem vfs,
        LocalizationManager localization,
        World world,
        SystemScheduler scheduler,
        GameHostServices host)
    {
        if (!Directory.Exists(modsRootDirectory))
            return;

        var dirs = Directory.GetDirectories(modsRootDirectory);
        var manifests = new List<(string Dir, ModManifest M)>();

        foreach (var dir in dirs)
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            var json = File.ReadAllText(manifestPath);
            var m = JsonSerializer.Deserialize<ModManifest>(json, JsonOptions);
            if (m is null || string.IsNullOrWhiteSpace(m.Id))
                continue;

            manifests.Add((dir, m));
        }

        foreach (var entry in manifests.OrderBy(t => t.M.LoadOrder).ThenBy(t => t.M.Id, StringComparer.Ordinal))
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

        foreach (var entry in manifests.OrderBy(t => t.M.LoadOrder).ThenBy(t => t.M.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.M.EntryAssembly))
                continue;

            var dll = Path.Combine(entry.Dir, entry.M.EntryAssembly);
            if (!File.Exists(dll))
                continue;

            var asm = Assembly.LoadFrom(dll);
            var modType = asm.GetExportedTypes()
                .FirstOrDefault(t => typeof(IMod).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

            if (modType is null)
                continue;

            var mod = (IMod)Activator.CreateInstance(modType)!;
            var ctx = new ModLoadContext(entry.M, entry.Dir, vfs, localization, world, scheduler, host);
            mod.OnLoad(ctx);
            _instances.Add(mod);
        }
    }

    public void UnloadAll()
    {
        for (var i = _instances.Count - 1; i >= 0; i--)
            _instances[i].OnUnload();

        _instances.Clear();
        _manifests.Clear();
    }
}
