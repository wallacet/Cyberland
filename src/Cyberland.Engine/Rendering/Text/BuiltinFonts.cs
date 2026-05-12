using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Embedded Apache-licensed Roboto subset faces shipped with the engine for UI text without mod assets.
/// </summary>
/// <remarks>
/// Attribution: see <c>Rendering/Text/Builtin/NOTICE.txt</c>. Mods should prefer
/// <see cref="FontLibrary.RegisterFamilyFromBytes"/> for branding; these ids remain stable for samples and tools.
/// </remarks>
public static class BuiltinFonts
{
    /// <summary>
    /// Virtual manifest paths for engine-shipped baked MSDF atlases.
    /// Mods should call <c>ModLoadContext.LoadBakedMsdfAtlas*</c> with these values.
    /// </summary>
    public static class BakedAtlasManifestPath
    {
        private const string Root = "_cyberland/engine/msdf/";

        /// <summary>UI sans regular 12 px baked atlas manifest path.</summary>
        public const string UiSansRegular12 = Root + "UiSansRegular12LatinExtended.manifest.json";
        /// <summary>UI sans regular 13 px baked atlas manifest path.</summary>
        public const string UiSansRegular13 = Root + "UiSansRegular13LatinExtended.manifest.json";
        /// <summary>UI sans regular 14 px baked atlas manifest path.</summary>
        public const string UiSansRegular14 = Root + "UiSansRegular14LatinExtended.manifest.json";
        /// <summary>UI sans regular 15 px baked atlas manifest path.</summary>
        public const string UiSansRegular15 = Root + "UiSansRegular15LatinExtended.manifest.json";
        /// <summary>UI sans regular 16 px baked atlas manifest path.</summary>
        public const string UiSansRegular16 = Root + "UiSansRegular16LatinExtended.manifest.json";
        /// <summary>UI sans regular 18 px baked atlas manifest path.</summary>
        public const string UiSansRegular18 = Root + "UiSansRegular18LatinExtended.manifest.json";
        /// <summary>UI sans regular 20 px baked atlas manifest path.</summary>
        public const string UiSansRegular20 = Root + "UiSansRegular20LatinExtended.manifest.json";
        /// <summary>UI sans regular 22 px baked atlas manifest path.</summary>
        public const string UiSansRegular22 = Root + "UiSansRegular22LatinExtended.manifest.json";
        /// <summary>UI sans regular 23 px baked atlas manifest path.</summary>
        public const string UiSansRegular23 = Root + "UiSansRegular23LatinExtended.manifest.json";
        /// <summary>UI sans regular 24 px baked atlas manifest path.</summary>
        public const string UiSansRegular24 = Root + "UiSansRegular24LatinExtended.manifest.json";
        /// <summary>UI sans bold 14 px baked atlas manifest path.</summary>
        public const string UiSansBold14 = Root + "UiSansBold14LatinExtended.manifest.json";
        /// <summary>UI sans bold 18 px baked atlas manifest path.</summary>
        public const string UiSansBold18 = Root + "UiSansBold18LatinExtended.manifest.json";
        /// <summary>UI sans bold 23 px baked atlas manifest path.</summary>
        public const string UiSansBold23 = Root + "UiSansBold23LatinExtended.manifest.json";
        /// <summary>Mono regular 14 px baked atlas manifest path.</summary>
        public const string MonoRegular14 = Root + "MonoRegular14LatinExtended.manifest.json";
        /// <summary>Mono regular 18 px baked atlas manifest path.</summary>
        public const string MonoRegular18 = Root + "MonoRegular18LatinExtended.manifest.json";
    }

    /// <summary>Logical id for the embedded UI sans face (Roboto subset).</summary>
    public const string UiSans = "cyberland.engine/ui";

    /// <summary>Logical id for the embedded monospace face (Roboto Mono subset).</summary>
    public const string Mono = "cyberland.engine/mono";

    private const string UiSansResource = "Cyberland.Engine.Rendering.Text.Builtin.Roboto-Regular.ttf";
    private const string MonoResource = "Cyberland.Engine.Rendering.Text.Builtin.RobotoMono-Regular.ttf";

    /// <summary>Lazy so atlas table rows are attributed to <see cref="CreateBakedAtlasResourceRows"/> under coverage (not the type initializer).</summary>
    private static readonly Lazy<BakedAtlasResourceRow[]> BakedAtlasResourcesLazy =
        new(CreateBakedAtlasResourceRows);

    private static BakedAtlasResourceRow[] BakedAtlasResources =>
        BakedAtlasResourcesLazy.Value;

    private readonly record struct BakedAtlasResourceRow(
        string Label,
        string ManifestSuffix,
        string PagePrefix,
        string VirtualManifestPath);

    private static BakedAtlasResourceRow[] CreateBakedAtlasResourceRows() =>
    [
        new("builtin-ui-regular12", "UiSansRegular12LatinExtended.manifest.json", "UiSansRegular12LatinExtended", BakedAtlasManifestPath.UiSansRegular12),
        new("builtin-ui-regular13", "UiSansRegular13LatinExtended.manifest.json", "UiSansRegular13LatinExtended", BakedAtlasManifestPath.UiSansRegular13),
        new("builtin-ui-regular14", "UiSansRegular14LatinExtended.manifest.json", "UiSansRegular14LatinExtended", BakedAtlasManifestPath.UiSansRegular14),
        new("builtin-ui-regular15", "UiSansRegular15LatinExtended.manifest.json", "UiSansRegular15LatinExtended", BakedAtlasManifestPath.UiSansRegular15),
        new("builtin-ui-regular16", "UiSansRegular16LatinExtended.manifest.json", "UiSansRegular16LatinExtended", BakedAtlasManifestPath.UiSansRegular16),
        new("builtin-ui-regular18", "UiSansRegular18LatinExtended.manifest.json", "UiSansRegular18LatinExtended", BakedAtlasManifestPath.UiSansRegular18),
        new("builtin-ui-regular20", "UiSansRegular20LatinExtended.manifest.json", "UiSansRegular20LatinExtended", BakedAtlasManifestPath.UiSansRegular20),
        new("builtin-ui-regular22", "UiSansRegular22LatinExtended.manifest.json", "UiSansRegular22LatinExtended", BakedAtlasManifestPath.UiSansRegular22),
        new("builtin-ui-regular23", "UiSansRegular23LatinExtended.manifest.json", "UiSansRegular23LatinExtended", BakedAtlasManifestPath.UiSansRegular23),
        new("builtin-ui-regular24", "UiSansRegular24LatinExtended.manifest.json", "UiSansRegular24LatinExtended", BakedAtlasManifestPath.UiSansRegular24),
        new("builtin-ui-bold14", "UiSansBold14LatinExtended.manifest.json", "UiSansBold14LatinExtended", BakedAtlasManifestPath.UiSansBold14),
        new("builtin-ui-bold18", "UiSansBold18LatinExtended.manifest.json", "UiSansBold18LatinExtended", BakedAtlasManifestPath.UiSansBold18),
        new("builtin-ui-bold23", "UiSansBold23LatinExtended.manifest.json", "UiSansBold23LatinExtended", BakedAtlasManifestPath.UiSansBold23),
        new("builtin-mono-regular14", "MonoRegular14LatinExtended.manifest.json", "MonoRegular14LatinExtended", BakedAtlasManifestPath.MonoRegular14),
        new("builtin-mono-regular18", "MonoRegular18LatinExtended.manifest.json", "MonoRegular18LatinExtended", BakedAtlasManifestPath.MonoRegular18)
    ];

    /// <summary>Registers both built-in families on <paramref name="library"/> (no-op if streams are missing).</summary>
    public static void AddTo(FontLibrary library)
    {
        if (library is null)
            throw new ArgumentNullException(nameof(library));

        var asm = Assembly.GetExecutingAssembly();
        if (Read(asm, UiSansResource) is { } ui)
            library.RegisterFamilyFromBytes(UiSans, ui);
        if (Read(asm, MonoResource) is { } mono)
            library.RegisterFamilyFromBytes(Mono, mono);
    }

    private static ReadOnlyMemory<byte>? Read(Assembly asm, string name)
    {
        var resolvedName = ResolveResourceName(asm, name);
        using var s = asm.GetManifestResourceStream(resolvedName);
        if (s is null)
            return null;

        using var ms = new MemoryStream((int)Math.Min(s.Length, int.MaxValue));
        s.CopyTo(ms);
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }

    private static string ResolveResourceName(Assembly asm, string suffix)
    {
        foreach (var res in asm.GetManifestResourceNames())
        {
            if (res.EndsWith(suffix, StringComparison.Ordinal))
                return res;
        }

        return suffix;
    }

    internal static IEnumerable<string> EnumerateBakedAtlasManifestPaths()
    {
        foreach (var atlas in BakedAtlasResources)
            yield return atlas.VirtualManifestPath;
    }

    [ExcludeFromCodeCoverage(Justification = "Embedded-resource fault branches are packaging-dependent; success paths are tested.")]
    internal static bool TryResolveBakedAtlasFromVirtualPath(
        string manifestPath,
        out BakedMsdfAtlasManifest manifest,
        out Func<string, byte[]> readPageBytes)
    {
        manifest = null!;
        readPageBytes = null!;

        var normalized = NormalizeVirtualPath(manifestPath);
        BakedAtlasResourceRow? match = null;
        foreach (var atlas in BakedAtlasResources)
        {
            if (string.Equals(atlas.VirtualManifestPath, normalized, StringComparison.OrdinalIgnoreCase))
            {
                match = atlas;
                break;
            }
        }

        if (match is null)
            return false;
        var row = match.Value;

        var asm = Assembly.GetExecutingAssembly();
        var manifestBytes = Read(asm, row.ManifestSuffix);
        if (manifestBytes is null)
            return false;

        var manifestJson = Encoding.UTF8.GetString(manifestBytes.Value.Span).TrimStart('\uFEFF');
        var parsed = JsonSerializer.Deserialize<BakedMsdfAtlasManifest>(manifestJson);
        if (parsed is null)
            return false;

        manifest = parsed;
        readPageBytes = relPath =>
        {
            var pageFile = Path.GetFileName(relPath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(pageFile))
                pageFile = "page0.png";
            var resourceName = $"{row.PagePrefix}.{pageFile}";
            var pageBytes = Read(asm, resourceName);
            if (pageBytes is null)
                throw new InvalidOperationException($"Unknown baked page '{relPath}' for {row.Label}.");
            return pageBytes.Value.ToArray();
        };
        return true;
    }

    internal static IEnumerable<(string Label, BakedMsdfAtlasManifest Manifest, Func<string, byte[]> ReadPageBytes)> EnumerateBakedAtlasResources()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var atlas in BakedAtlasResources)
        {
            foreach (var entry in EnumerateSingleAtlas(asm, atlas.Label, atlas.ManifestSuffix, atlas.PagePrefix))
                yield return entry;
        }
    }

    private static IEnumerable<(string Label, BakedMsdfAtlasManifest Manifest, Func<string, byte[]> ReadPageBytes)> EnumerateSingleAtlas(
        Assembly asm,
        string label,
        string manifestResourceName,
        string pagePrefix)
    {
        var manifestBytes = Read(asm, manifestResourceName);
        if (manifestBytes is null)
            yield break;

        var manifestJson = Encoding.UTF8.GetString(manifestBytes.Value.Span).TrimStart('\uFEFF');
        var manifest = JsonSerializer.Deserialize<BakedMsdfAtlasManifest>(manifestJson);
        if (manifest is null)
            yield break;

        yield return (
            label,
            manifest,
            relPath =>
            {
                var suffix = relPath.StartsWith("page", StringComparison.OrdinalIgnoreCase)
                    ? relPath
                    : $"page0.png";
                var resourceName = $"{pagePrefix}.{suffix}";
                var pageBytes = Read(asm, resourceName);
                if (pageBytes is null)
                    throw new InvalidOperationException($"Unknown baked page '{relPath}' for {label}.");
                return pageBytes.Value.ToArray();
            });
    }

    private static string NormalizeVirtualPath(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/');
}
