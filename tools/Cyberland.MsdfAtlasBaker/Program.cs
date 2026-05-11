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

BakeFamily("UiSansRegular12LatinExtended", BuiltinFonts.UiSans, 12f, false, false);
BakeFamily("UiSansRegular13LatinExtended", BuiltinFonts.UiSans, 13f, false, false);
BakeFamily("UiSansRegular14LatinExtended", BuiltinFonts.UiSans, 14f, false, false);
BakeFamily("UiSansRegular15LatinExtended", BuiltinFonts.UiSans, 15f, false, false);
BakeFamily("UiSansRegular18LatinExtended", BuiltinFonts.UiSans, 18f, false, false);
BakeFamily("UiSansRegular22LatinExtended", BuiltinFonts.UiSans, 22f, false, false);
BakeFamily("UiSansRegular23LatinExtended", BuiltinFonts.UiSans, 23f, false, false);
BakeFamily("UiSansRegular24LatinExtended", BuiltinFonts.UiSans, 24f, false, false);
BakeFamily("UiSansBold14LatinExtended", BuiltinFonts.UiSans, 14f, true, false);
BakeFamily("UiSansBold18LatinExtended", BuiltinFonts.UiSans, 18f, true, false);
BakeFamily("UiSansBold23LatinExtended", BuiltinFonts.UiSans, 23f, true, false);
BakeFamily("MonoRegular14LatinExtended", BuiltinFonts.Mono, 14f, false, false);

Console.WriteLine($"Baked atlases written to: {outputDir}");

void BakeFamily(string atlasName, string familyId, float sizePx, bool bold, bool italic)
{
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
    cpCount += BakeCodePoint(0x2014);

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

    var atlasBase = Path.Combine(outputDir, atlasName);
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
