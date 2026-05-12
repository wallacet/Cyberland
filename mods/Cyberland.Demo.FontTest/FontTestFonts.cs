namespace Cyberland.Demo.FontTest;

/// <summary>
/// Custom (mod-registered) font used only by this demo: Jost from Indestructible Type, SIL Open Font License 1.1.
/// TTF sources live under <c>Content/Fonts/Source/</c>; MSDF atlases under <c>Content/Fonts/Baked/</c> (see <c>Cyberland.MsdfAtlasBaker</c>).
/// </summary>
public static class FontTestFonts
{
    /// <summary>Logical family id registered on <see cref="Cyberland.Engine.Rendering.Text.FontLibrary"/> at mod load.</summary>
    public const string JostFamilyId = "fonttest.jost";

    /// <summary>Paths relative to this mod’s <c>Content/</c> root (VFS after <see cref="Cyberland.Engine.Modding.ModLoadContext.MountDefaultContent"/>).</summary>
    public const string JostRegularVfsPath = "Fonts/Source/Jost-Regular.ttf";

    public const string JostBoldVfsPath = "Fonts/Source/Jost-Bold.ttf";

    /// <summary>Pre-baked MSDF manifest paths for every Jost size baked by the repo baker (regular + bold where applicable).</summary>
    public static readonly string[] BakedJostManifestVfsPaths =
    [
        "Fonts/Baked/FontTestJostRegular12LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular13LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular14LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular15LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular16LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular17LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular18LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular20LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular22LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular23LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostRegular24LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostBold14LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostBold18LatinExtended.manifest.json",
        "Fonts/Baked/FontTestJostBold23LatinExtended.manifest.json"
    ];
}
