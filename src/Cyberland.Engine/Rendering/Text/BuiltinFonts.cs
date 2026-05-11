using System.Reflection;
using System.Text;
using System.Text.Json;

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
    /// <summary>Logical id for the embedded UI sans face (Roboto subset).</summary>
    public const string UiSans = "cyberland.engine/ui";

    /// <summary>Logical id for the embedded monospace face (Roboto Mono subset).</summary>
    public const string Mono = "cyberland.engine/mono";

    private const string UiSansResource = "Cyberland.Engine.Rendering.Text.Builtin.Roboto-Regular.ttf";
    private const string MonoResource = "Cyberland.Engine.Rendering.Text.Builtin.RobotoMono-Regular.ttf";

    /// <summary>Lazy so atlas table rows are attributed to <see cref="CreateBakedAtlasResourceRows"/> under coverage (not the type initializer).</summary>
    private static readonly Lazy<(string Label, string ManifestSuffix, string PagePrefix)[]> BakedAtlasResourcesLazy =
        new(CreateBakedAtlasResourceRows);

    private static (string Label, string ManifestSuffix, string PagePrefix)[] BakedAtlasResources =>
        BakedAtlasResourcesLazy.Value;

    private static (string Label, string ManifestSuffix, string PagePrefix)[] CreateBakedAtlasResourceRows() =>
    [
        ("builtin-ui-regular12", "UiSansRegular12LatinExtended.manifest.json", "UiSansRegular12LatinExtended"),
        ("builtin-ui-regular13", "UiSansRegular13LatinExtended.manifest.json", "UiSansRegular13LatinExtended"),
        ("builtin-ui-regular14", "UiSansRegular14LatinExtended.manifest.json", "UiSansRegular14LatinExtended"),
        ("builtin-ui-regular15", "UiSansRegular15LatinExtended.manifest.json", "UiSansRegular15LatinExtended"),
        ("builtin-ui-regular16", "UiSansRegular16LatinExtended.manifest.json", "UiSansRegular16LatinExtended"),
        ("builtin-ui-regular18", "UiSansRegular18LatinExtended.manifest.json", "UiSansRegular18LatinExtended"),
        ("builtin-ui-regular20", "UiSansRegular20LatinExtended.manifest.json", "UiSansRegular20LatinExtended"),
        ("builtin-ui-regular22", "UiSansRegular22LatinExtended.manifest.json", "UiSansRegular22LatinExtended"),
        ("builtin-ui-regular23", "UiSansRegular23LatinExtended.manifest.json", "UiSansRegular23LatinExtended"),
        ("builtin-ui-regular24", "UiSansRegular24LatinExtended.manifest.json", "UiSansRegular24LatinExtended"),
        ("builtin-ui-bold14", "UiSansBold14LatinExtended.manifest.json", "UiSansBold14LatinExtended"),
        ("builtin-ui-bold18", "UiSansBold18LatinExtended.manifest.json", "UiSansBold18LatinExtended"),
        ("builtin-ui-bold23", "UiSansBold23LatinExtended.manifest.json", "UiSansBold23LatinExtended"),
        ("builtin-mono-regular14", "MonoRegular14LatinExtended.manifest.json", "MonoRegular14LatinExtended"),
        ("builtin-mono-regular18", "MonoRegular18LatinExtended.manifest.json", "MonoRegular18LatinExtended")
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
}
