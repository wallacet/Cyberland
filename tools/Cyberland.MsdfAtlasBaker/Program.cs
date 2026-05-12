using System.Text;
using System.Text.Json;
using Cyberland.Engine.Rendering.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

var outputDir = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Cyberland.Engine", "Rendering", "Text", "Baked"));
Directory.CreateDirectory(outputDir);

var fonts = new FontLibrary();
BuiltinFonts.AddTo(fonts);

// General punctuation above U+024F (not in the main Latin loop). Baked first so a low page budget still loads them from page 0.
var bakedExtraPunctuation = new[]
{
    0x2013, 0x2014, 0x2015, // en dash, em dash, horizontal bar
    0x2018, 0x2019, 0x201C, 0x201D, // quotes
    0x2022, 0x2026, // bullet, ellipsis
    0x2039, 0x203A, // single guillemets
    0x2044, // fraction slash
    0x20AC // euro
};

BakeFamily(outputDir, "UiSansRegular12LatinExtended", BuiltinFonts.UiSans, 12f, false, false);
BakeFamily(outputDir, "UiSansRegular13LatinExtended", BuiltinFonts.UiSans, 13f, false, false);
BakeFamily(outputDir, "UiSansRegular14LatinExtended", BuiltinFonts.UiSans, 14f, false, false);
BakeFamily(outputDir, "UiSansRegular15LatinExtended", BuiltinFonts.UiSans, 15f, false, false);
BakeFamily(outputDir, "UiSansRegular16LatinExtended", BuiltinFonts.UiSans, 16f, false, false);
BakeFamily(outputDir, "UiSansRegular18LatinExtended", BuiltinFonts.UiSans, 18f, false, false);
BakeFamily(outputDir, "UiSansRegular20LatinExtended", BuiltinFonts.UiSans, 20f, false, false);
BakeFamily(outputDir, "UiSansRegular22LatinExtended", BuiltinFonts.UiSans, 22f, false, false);
BakeFamily(outputDir, "UiSansRegular23LatinExtended", BuiltinFonts.UiSans, 23f, false, false);
BakeFamily(outputDir, "UiSansRegular24LatinExtended", BuiltinFonts.UiSans, 24f, false, false);
BakeFamily(outputDir, "UiSansBold14LatinExtended", BuiltinFonts.UiSans, 14f, true, false);
BakeFamily(outputDir, "UiSansBold18LatinExtended", BuiltinFonts.UiSans, 18f, true, false);
BakeFamily(outputDir, "UiSansBold23LatinExtended", BuiltinFonts.UiSans, 23f, true, false);
BakeFamily(outputDir, "MonoRegular14LatinExtended", BuiltinFonts.Mono, 14f, false, false);
BakeFamily(outputDir, "MonoRegular18LatinExtended", BuiltinFonts.Mono, 18f, false, false);

Console.WriteLine($"Baked atlases written to: {outputDir}");

// -------------------------------------------------------------------------
// FontTest mod: Jost (SIL OFL) — custom family id, manifests under mod Content (not engine builtins).
// -------------------------------------------------------------------------
var repoRoot = FindRepoRoot();
var fontTestSourceDir = Path.Combine(repoRoot, "mods", "Cyberland.Demo.FontTest", "Content", "Fonts", "Source");
var fontTestBakedDir = Path.Combine(repoRoot, "mods", "Cyberland.Demo.FontTest", "Content", "Fonts", "Baked");
var jostRegular = Path.Combine(fontTestSourceDir, "Jost-Regular.ttf");
var jostBold = Path.Combine(fontTestSourceDir, "Jost-Bold.ttf");
const string FontTestJostFamilyId = "fonttest.jost";

if (File.Exists(jostRegular))
{
    var regBytes = File.ReadAllBytes(jostRegular);
    ReadOnlyMemory<byte>? boldMem = File.Exists(jostBold) ? File.ReadAllBytes(jostBold) : null;
    fonts.RegisterFamilyFromBytes(FontTestJostFamilyId, regBytes, boldMem, null, null);
    Directory.CreateDirectory(fontTestBakedDir);

    // Same pixel sizes as built-in UiSans regular coverage, plus one non-standard (17) for custom-path stress.
    foreach (var px in new[] { 12f, 13f, 14f, 15f, 16f, 17f, 18f, 20f, 22f, 23f, 24f })
        BakeFamily(fontTestBakedDir, $"FontTestJostRegular{MathF.Round(px):0}LatinExtended", FontTestJostFamilyId, px, false,
            false);
    foreach (var px in new[] { 14f, 18f, 23f })
        BakeFamily(fontTestBakedDir, $"FontTestJostBold{MathF.Round(px):0}LatinExtended", FontTestJostFamilyId, px, true,
            false);

    Console.WriteLine($"FontTest Jost atlases written to: {fontTestBakedDir}");
}
else
{
    Console.WriteLine(
        $"FontTest Jost bake skipped (missing {jostRegular}). Add Jost-Regular.ttf (and optional Jost-Bold.ttf) under Source, then re-run.");
}

static string FindRepoRoot()
{
    var dir = Path.GetFullPath(AppContext.BaseDirectory);
    for (var i = 0; i < 12; i++)
    {
        if (File.Exists(Path.Combine(dir, "Cyberland.sln")) && Directory.Exists(Path.Combine(dir, "mods")))
            return dir;
        var parent = Directory.GetParent(dir);
        if (parent is null)
            break;
        dir = parent.FullName;
    }

    throw new InvalidOperationException(
        "Could not locate repository root (expected Cyberland.sln and mods/ while walking up from the baker output directory).");
}

void BakeFamily(string bakeRoot, string atlasName, string familyId, float sizePx, bool bold, bool italic)
{
    Directory.CreateDirectory(bakeRoot);
    var style = new TextStyle(familyId, sizePx, default, bold, italic);
    var pages = new List<byte[]>();
    byte[] page = NewPage();
    pages.Add(page);
    var x = 0;
    var y = 0;
    var rowHeight = 0;
    var glyphs = new List<BakedMsdfGlyphEntry>(512);
    var cpCount = 0;
    var glyphChars = new char[2];
    foreach (var codePoint in bakedExtraPunctuation)
        cpCount += BakeCodePoint(codePoint);

    for (var codePoint = 0x20; codePoint <= 0x024F; codePoint++)
    {
        var rune = new Rune(codePoint);
        var glyphLen = rune.EncodeToUtf16(glyphChars);
        byte[]? rgba;
        int w, h;
        float drawW, drawH, adv, cx, cyW, range;
        lock (fonts.FontRasterSync)
        {
            _ = fonts.TryCreateFontUnlocked(in style, out var font, out _);
            if (!GlyphRasterizer.TryCreateGlyphMsdf(font, glyphChars.AsSpan(0, glyphLen), out rgba, out w, out h,
                    out drawW, out drawH, out adv, out cx, out cyW, out range))
            {
                continue;
            }
        }

        if (x + w > GlyphAtlasPage.SizePx)
        {
            x = 0;
            y += rowHeight;
            rowHeight = 0;
        }

        if (y + h > GlyphAtlasPage.SizePx)
        {
            page = NewPage();
            pages.Add(page);
            x = 0;
            y = 0;
            rowHeight = 0;
        }

        GlyphAtlasPage.BlitPremultiplied(page, GlyphAtlasPage.SizePx, x, y, rgba!, w, h);
        glyphs.Add(new BakedMsdfGlyphEntry
        {
            CodePoint = codePoint,
            PageIndex = pages.Count - 1,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            DrawWidthPx = drawW,
            DrawHeightPx = drawH,
            OffsetPenToCenterX = cx,
            OffsetPenToCenterYWorld = cyW,
            AdvancePx = adv,
            MsdfPixelRange = range
        });
        x += w + 1;
        rowHeight = Math.Max(rowHeight, h + 1);
        cpCount++;
    }

    var manifest = new BakedMsdfAtlasManifest
    {
        Version = 1,
        FamilyId = familyId,
        Face = bold && italic ? "BoldItalic" : bold ? "Bold" : italic ? "Italic" : "Regular",
        SizePixels = sizePx,
        RasterRevision = GlyphRasterizer.RasterRevision,
        PageSizePixels = GlyphAtlasPage.SizePx,
        Pages = Enumerable.Range(0, pages.Count)
            .Select(i => new BakedMsdfAtlasPageRef { Path = $"page{i}.png" })
            .ToArray(),
        Glyphs = glyphs.ToArray()
    };

    var atlasBase = Path.Combine(bakeRoot, atlasName);
    for (var i = 0; i < pages.Count; i++)
    {
        var pagePath = $"{atlasBase}.page{i}.png";
        using var image = Image.LoadPixelData<Rgba32>(pages[i], GlyphAtlasPage.SizePx, GlyphAtlasPage.SizePx);
        image.SaveAsPng(pagePath);
    }

    var manifestPath = atlasBase + ".manifest.json";
    var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(manifestPath, json, new UTF8Encoding(false));
    Console.WriteLine($"{atlasName}: glyphs={cpCount} pages={pages.Count} manifest={manifestPath}");

    static byte[] NewPage() => new byte[GlyphAtlasPage.SizePx * GlyphAtlasPage.SizePx * 4];

    int BakeCodePoint(int codePoint)
    {
        var rune = new Rune(codePoint);
        var glyphLen = rune.EncodeToUtf16(glyphChars);
        byte[]? rgba;
        int w, h;
        float drawW, drawH, adv, cx, cyW, range;
        lock (fonts.FontRasterSync)
        {
            _ = fonts.TryCreateFontUnlocked(in style, out var font, out _);
            if (!GlyphRasterizer.TryCreateGlyphMsdf(font, glyphChars.AsSpan(0, glyphLen), out rgba, out w, out h,
                    out drawW, out drawH, out adv, out cx, out cyW, out range))
            {
                return 0;
            }
        }

        if (x + w > GlyphAtlasPage.SizePx)
        {
            x = 0;
            y += rowHeight;
            rowHeight = 0;
        }

        if (y + h > GlyphAtlasPage.SizePx)
        {
            page = NewPage();
            pages.Add(page);
            x = 0;
            y = 0;
            rowHeight = 0;
        }

        GlyphAtlasPage.BlitPremultiplied(page, GlyphAtlasPage.SizePx, x, y, rgba!, w, h);
        glyphs.Add(new BakedMsdfGlyphEntry
        {
            CodePoint = codePoint,
            PageIndex = pages.Count - 1,
            X = x,
            Y = y,
            Width = w,
            Height = h,
            DrawWidthPx = drawW,
            DrawHeightPx = drawH,
            OffsetPenToCenterX = cx,
            OffsetPenToCenterYWorld = cyW,
            AdvancePx = adv,
            MsdfPixelRange = range
        });
        x += w + 1;
        rowHeight = Math.Max(rowHeight, h + 1);
        return 1;
    }
}
