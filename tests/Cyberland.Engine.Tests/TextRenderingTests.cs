using Cyberland.Engine.Assets;
using Cyberland.Engine.Localization;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using SixLabors.Fonts;
using Silk.NET.Maths;
using TextRenderer = Cyberland.Engine.Rendering.Text.TextRenderer;
using TextRun = Cyberland.Engine.Rendering.Text.TextRun;

namespace Cyberland.Engine.Tests;

public sealed class TextRenderingTests
{
    [Fact]
    public void BuiltinFonts_AddTo_registers_ui_and_mono()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        Assert.True(lib.TryGetFamily(BuiltinFonts.UiSans, out var ui) && ui is not null);
        Assert.True(lib.TryGetFamily(BuiltinFonts.Mono, out var mono) && mono is not null);
    }

    [Fact]
    public void BuiltinFonts_AddTo_null_throws() =>
        Assert.Throws<ArgumentNullException>(() => BuiltinFonts.AddTo(null!));

    [Fact]
    public void FontLibrary_RegisterFamilyFromBytes_duplicate_throws()
    {
        var lib = new FontLibrary();
        var bytes = LoadRobotoUi();
        lib.RegisterFamilyFromBytes("dup", bytes);
        Assert.Throws<InvalidOperationException>(() => lib.RegisterFamilyFromBytes("dup", bytes));
    }

    [Fact]
    public void FontLibrary_RegisterFamilyFromBytes_empty_id_throws()
    {
        var lib = new FontLibrary();
        Assert.Throws<ArgumentException>(() => lib.RegisterFamilyFromBytes("", LoadRobotoUi()));
    }

    [Fact]
    public void FontLibrary_TryGetFamily_null_id_returns_false()
    {
        var lib = new FontLibrary();
        Assert.False(lib.TryGetFamily(null!, out _));
    }

    [Fact]
    public async Task FontLibrary_RegisterFamilyFromVirtualPathsAsync_loads_faces()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb font " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var regPath = Path.Combine(root, "r.ttf");
        var boldPath = Path.Combine(root, "b.ttf");
        var bytes = LoadRobotoUi().ToArray();
        await File.WriteAllBytesAsync(regPath, bytes);
        await File.WriteAllBytesAsync(boldPath, bytes);

        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var lib = new FontLibrary();
            await lib.RegisterFamilyFromVirtualPathsAsync(assets, "vfs", "r.ttf", boldPath: "b.ttf");
            Assert.True(lib.TryGetFamily("vfs", out var f) && f is not null);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TextGlyphCache_Clear_is_idempotent()
    {
        var c = new TextGlyphCache();
        c.Clear();
        c.Clear();
    }

    [Fact]
    public void TextRenderer_DrawLiteral_and_Localized_and_Runs_submit_sprites()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"k":"Hello"}"""u8.ToArray());

        var st = new TextStyle(BuiltinFonts.UiSans, 18f, new Vector4D<float>(1f, 1f, 1f, 1f));
        TextRenderer.DrawLiteral(r, lib, cache, st, "A", new Vector2D<float>(10f, 50f));
        TextRenderer.DrawLocalized(r, lib, cache, loc, st, "k", new Vector2D<float>(10f, 80f));
        TextRenderer.DrawRuns(r, lib, cache, loc,
            new[]
            {
                new TextRun("X", st with { Color = new Vector4D<float>(1f, 0f, 0f, 1f) }),
                new TextRun("k", st with { Underline = true, Strikethrough = true }, isLocalizationKey: true)
            },
            new Vector2D<float>(5f, 100f));

        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void TextRenderer_skips_when_renderer_null_or_empty()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, new Vector4D<float>(1f, 0f, 0f, 1f));
        TextRenderer.DrawLiteral(null!, lib, cache, st, "a", default);
        TextRenderer.DrawLiteral(new RecordingRenderer(), lib, cache, st, "", default);
        TextRenderer.DrawLocalized(new RecordingRenderer(), lib, cache, null!, st, "k", default);
        TextRenderer.DrawRuns(new RecordingRenderer(), lib, cache, null, ReadOnlySpan<TextRun>.Empty, default);
    }

    [Fact]
    public void GlyphRasterizer_whitespace_and_letter()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        Assert.True(lib.TryCreateFont(
            new TextStyle(BuiltinFonts.UiSans, 20f, Vector4D<float>.One),
            out var font,
            out _));

        Assert.True(GlyphRasterizer.TryCreateGlyphRgba(font, " ", out var ws, out var ww, out var wh, out _, out _, out _));
        Assert.True(ww >= 1 && wh >= 1);
        Assert.NotNull(ws);

        Assert.True(GlyphRasterizer.TryCreateGlyphRgba(font, "Q", out var g, out var w, out var h, out _, out _, out _));
        Assert.True(w > 1 && h > 1);
        Assert.NotNull(g);
    }

    [Fact]
    public void GlyphRasterizer_rejects_empty_glyph_string() =>
        Assert.False(GlyphRasterizer.TryCreateGlyphRgba(
            CreateTestFont(), "", out _, out _, out _, out _, out _, out _));

    [Fact]
    public void RegisteredFamily_GetFace_covers_all_kinds()
    {
        var lib = new FontLibrary();
        var b = LoadRobotoUi();
        lib.RegisterFamilyFromBytes("m", b, bold: b, italic: b, boldItalic: b);
        Assert.True(lib.TryGetFamily("m", out var fam) && fam is not null);
        _ = fam!.GetFace(FontFaceKind.Regular);
        _ = fam.GetFace(FontFaceKind.Bold);
        _ = fam.GetFace(FontFaceKind.Italic);
        _ = fam.GetFace(FontFaceKind.BoldItalic);
    }

    [Fact]
    public void FontLibrary_SelectFace_prefers_bold_italic_chain()
    {
        var lib = new FontLibrary();
        var b = LoadRobotoUi();
        lib.RegisterFamilyFromBytes("x", b, bold: b, italic: b);
        Assert.True(lib.TryCreateFont(new TextStyle("x", 14f, Vector4D<float>.One, Bold: true, Italic: true),
            out _, out var face));
        Assert.Equal(FontFaceKind.Bold, face);
    }

    private static ReadOnlyMemory<byte> LoadRobotoUi()
    {
        using var s = typeof(BuiltinFonts).Assembly.GetManifestResourceStream(
            "Cyberland.Engine.Rendering.Text.Builtin.Roboto-Regular.ttf")!;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }

    private static Font CreateTestFont()
    {
        var lib = new FontLibrary();
        lib.RegisterFamilyFromBytes("t", LoadRobotoUi());
        Assert.True(lib.TryCreateFont(new TextStyle("t", 12f, Vector4D<float>.One), out var f, out _));
        return f;
    }

    [Fact]
    public void BuiltinFonts_Read_returns_null_for_missing_resource()
    {
        var read = typeof(BuiltinFonts).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(read);
        Assert.Null(read!.Invoke(null, new object?[] { typeof(BuiltinFonts).Assembly, "No.Such.Resource" }));
        Assert.Null(read.Invoke(null, new object?[] { typeof(string).Assembly, "Any.Name" }));
    }

    [Fact]
    public void RegisteredFamily_GetFace_bolditalic_falls_back_when_face_missing()
    {
        var raw = LoadRobotoUi().ToArray();
        var col = new FontCollection();
        using var ms = new MemoryStream(raw);
        var fam = col.Add(ms, CultureInfo.InvariantCulture);
        var onlyBold = new FontLibrary.RegisteredFamily(fam, fam, null, null);
        Assert.Equal(fam, onlyBold.GetFace(FontFaceKind.BoldItalic));
        var onlyItalic = new FontLibrary.RegisteredFamily(fam, null, fam, null);
        Assert.Equal(fam, onlyItalic.GetFace(FontFaceKind.BoldItalic));
        var regOnly = new FontLibrary.RegisteredFamily(fam, null, null, null);
        Assert.Equal(fam, regOnly.GetFace(FontFaceKind.BoldItalic));
    }

    [Fact]
    public async Task FontLibrary_RegisterFamilyFromVirtualPathsAsync_loads_all_optional_faces()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb font4 " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var raw = LoadRobotoUi().ToArray();
        await File.WriteAllBytesAsync(Path.Combine(root, "r.ttf"), raw);
        await File.WriteAllBytesAsync(Path.Combine(root, "b.ttf"), raw);
        await File.WriteAllBytesAsync(Path.Combine(root, "i.ttf"), raw);
        await File.WriteAllBytesAsync(Path.Combine(root, "bi.ttf"), raw);
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var lib = new FontLibrary();
            await lib.RegisterFamilyFromVirtualPathsAsync(assets, "full", "r.ttf", "b.ttf", "i.ttf", "bi.ttf");
            Assert.True(lib.TryCreateFont(
                new TextStyle("full", 11f, Vector4D<float>.One, Bold: true, Italic: true), out _, out var fk));
            Assert.Equal(FontFaceKind.BoldItalic, fk);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TextGlyphCache_RegisterTexture_failure_returns_false()
    {
        var r = new RecordingRenderer { RegisterTextureRgbaOverride = -1 };
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 14f, Vector4D<float>.One);
        Assert.False(cache.TryGetGlyph(r, lib, st, 'Z', "Z", out _));
    }

    [Fact]
    public void TextGlyphCache_unknown_family_returns_false()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        var cache = new TextGlyphCache();
        Assert.False(cache.TryGetGlyph(r, lib, new TextStyle("nope", 12f, Vector4D<float>.One), 'A', "A", out _));
    }

    [Fact]
    public void TextGlyphCache_empty_grapheme_returns_false_after_font_resolution()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        Assert.False(cache.TryGetGlyph(r, lib, st, 32, "", out _));
    }

    [Fact]
    public void TextRenderer_DrawRuns_localization_key_without_manager_uses_raw_key()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        TextRenderer.DrawRuns(r, lib, cache, null, new[] { new TextRun("missing.key", st, true) },
            new Vector2D<float>(4f, 40f));
        Assert.NotEmpty(r.Sprites);
    }

    [Fact]
    public void GlyphRasterizer_rejects_empty_string()
    {
        var f = CreateTestFont();
        Assert.False(GlyphRasterizer.TryCreateGlyphRgba(f, "", out _, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void FontLibrary_SelectFace_bolditalic_falls_back_to_regular_when_only_regular_registered()
    {
        var lib = new FontLibrary();
        lib.RegisterFamilyFromBytes("reg", LoadRobotoUi());
        Assert.True(lib.TryCreateFont(
            new TextStyle("reg", 12f, Vector4D<float>.One, Bold: true, Italic: true), out _, out var fk));
        Assert.Equal(FontFaceKind.Regular, fk);
    }

    [Fact]
    public void FontLibrary_SelectFace_variants_cover_branches()
    {
        var raw = LoadRobotoUi();
        var lib = new FontLibrary();
        lib.RegisterFamilyFromBytes("bi", raw, bold: raw, italic: raw, boldItalic: raw);
        Assert.True(lib.TryCreateFont(new TextStyle("bi", 13f, Vector4D<float>.One, Bold: true, Italic: true),
            out _, out var fk));
        Assert.Equal(FontFaceKind.BoldItalic, fk);

        lib.RegisterFamilyFromBytes("io", raw, italic: raw);
        Assert.True(lib.TryCreateFont(new TextStyle("io", 13f, Vector4D<float>.One, Bold: true, Italic: true),
            out _, out fk));
        Assert.Equal(FontFaceKind.Italic, fk);

        lib.RegisterFamilyFromBytes("bo", raw, bold: raw);
        Assert.True(lib.TryCreateFont(new TextStyle("bo", 13f, Vector4D<float>.One, Bold: true, Italic: false),
            out _, out fk));
        Assert.Equal(FontFaceKind.Bold, fk);

        lib.RegisterFamilyFromBytes("it", raw, italic: raw);
        Assert.True(lib.TryCreateFont(new TextStyle("it", 13f, Vector4D<float>.One, Bold: false, Italic: true),
            out _, out fk));
        Assert.Equal(FontFaceKind.Italic, fk);
    }

    [Fact]
    public void TextRenderer_DrawLocalized_empty_key_early_out()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var loc = new LocalizationManager();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        TextRenderer.DrawLocalized(r, lib, cache, loc, st, "", new Vector2D<float>(0f, 0f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderer_DrawLocalized_resolved_empty_string_early_out()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"blank":""}"""u8.ToArray());
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        TextRenderer.DrawLocalized(r, lib, cache, loc, st, "blank", new Vector2D<float>(1f, 2f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderer_skips_glyph_when_upload_fails_but_continues_loop()
    {
        var r = new RecordingRenderer { RegisterTextureRgbaOverride = -1 };
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        TextRenderer.DrawLiteral(r, lib, cache, st, "XY", new Vector2D<float>(0f, 10f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextRenderer_skips_run_when_localized_resolves_empty()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var loc = new LocalizationManager();
        loc.MergeJson("""{"empty":""}"""u8.ToArray());
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        TextRenderer.DrawRuns(r, lib, cache, loc, new[] { new TextRun("empty", st, true) },
            new Vector2D<float>(0f, 20f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextGlyphCache_TryGetGlyph_null_renderer_returns_false()
    {
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 12f, Vector4D<float>.One);
        Assert.False(cache.TryGetGlyph(null!, lib, st, 'A', "A", out _));
    }

    [Fact]
    public void Atlas_second_glyph_triggers_subregion_upload()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 14f, Vector4D<float>.One);
        TextRenderer.DrawLiteral(r, lib, cache, st, "AB", new Vector2D<float>(0f, 20f));
        Assert.True(r.UploadSubregionCount >= 1);
        Assert.Equal(2, r.Sprites.Count);
        Assert.Equal(r.Sprites[0].AlbedoTextureId, r.Sprites[1].AlbedoTextureId);
        Assert.NotEqual(r.Sprites[0].UvRect.X, r.Sprites[1].UvRect.X);
    }

    [Fact]
    public void TextGlyphCache_subregion_upload_failure_skips_glyph()
    {
        var r = new RecordingRenderer { FailSubregionUpload = true };
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 14f, Vector4D<float>.One);
        TextRenderer.DrawLiteral(r, lib, cache, st, "AB", new Vector2D<float>(0f, 20f));
        Assert.Single(r.Sprites);
    }

    [Fact]
    public void GlyphAtlasPage_allocates_row_and_rejects_oversized()
    {
        var p = new GlyphAtlasPage();
        Assert.False(p.TryAllocate(GlyphAtlasPage.SizePx + 1, 4, out _, out _));
        Assert.True(p.TryAllocate(100, 10, out var x0, out var y0));
        Assert.True(p.TryAllocate(100, 10, out var x1, out var y1));
        Assert.NotEqual(x0, x1);
        Assert.Equal(y0, y1);
    }

    [Fact]
    public void FontLibrary_QuantizeEmSizePixels_roundtrip()
    {
        var q = FontLibrary.QuantizeEmSizePixels(18.25f);
        Assert.Equal(18.25f, FontLibrary.EmQuantToPixels(q), 3);
    }

    [Fact]
    public void FontLibrary_TryCreateFont_unknown_family_returns_false()
    {
        var lib = new FontLibrary();
        Assert.False(lib.TryCreateFont(new TextStyle("missing.family", 12f, Vector4D<float>.One), out _, out _));
    }

    [Fact]
    public void TextGlyphCache_TryPackAndUpload_rejects_glyph_larger_than_atlas_page()
    {
        var cache = new TextGlyphCache();
        var r = new RecordingRenderer();
        var rgba = new byte[GlyphAtlasPage.SizePx * GlyphAtlasPage.SizePx * 4];
        Assert.False(cache.TryPackAndUpload(r, rgba, GlyphAtlasPage.SizePx + 50, 8, out _, out _));
    }

    [Fact]
    public void FontLibrary_QuantizeEmSizePixels_clamps_extremes()
    {
        var lo = FontLibrary.QuantizeEmSizePixels(0.0001f);
        var hi = FontLibrary.QuantizeEmSizePixels(100_000f);
        Assert.True(lo > 0);
        Assert.True(hi > lo);
    }

    [Fact]
    public void GlyphAtlasPage_rejects_invalid_and_wraps_row()
    {
        Assert.False(new GlyphAtlasPage().TryAllocate(0, 4, out _, out _));
        Assert.False(new GlyphAtlasPage().TryAllocate(4, 0, out _, out _));

        var p = new GlyphAtlasPage();
        Assert.True(p.TryAllocate(1500, 10, out _, out _));
        Assert.True(p.TryAllocate(600, 10, out var xa, out var ya));
        Assert.Equal(GlyphAtlasPage.PadPx, xa);
        Assert.Equal(GlyphAtlasPage.PadPx + 10 + GlyphAtlasPage.PadPx, ya);
    }

    [Fact]
    public void GlyphAtlasPage_BlitPremultiplied_copies_rows()
    {
        var dst = new byte[20 * 20 * 4];
        var src = new byte[2 * 2 * 4];
        src[0] = 11;
        src[4] = 22;
        src[8] = 33;
        src[12] = 44;
        GlyphAtlasPage.BlitPremultiplied(dst, 20, 3, 4, src, 2, 2);
        Assert.Equal(11, dst[(4 * 20 + 3) * 4]);
        Assert.Equal(44, dst[(5 * 20 + 4) * 4]);
    }

    [Fact]
    public void TextRenderer_skips_invalid_utf16_lone_surrogate()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();
        var st = new TextStyle(BuiltinFonts.UiSans, 14f, Vector4D<float>.One);
        TextRenderer.DrawLiteral(r, lib, cache, st, "\uD800", new Vector2D<float>(0f, 10f));
        Assert.Empty(r.Sprites);
    }

    [Fact]
    public void TextGlyphCache_opens_new_atlas_page_when_previous_full()
    {
        var r = new RecordingRenderer();
        var lib = new FontLibrary();
        BuiltinFonts.AddTo(lib);
        var cache = new TextGlyphCache();

        var pagesField = typeof(TextGlyphCache).GetField("_pages", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(pagesField);
        var list = (List<GlyphAtlasPage>)pagesField!.GetValue(cache)!;
        var full = new GlyphAtlasPage();
        var gt = typeof(GlyphAtlasPage);
        gt.GetField("_cursorY", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(full, GlyphAtlasPage.SizePx - 4);
        gt.GetField("_cursorX", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(full, GlyphAtlasPage.PadPx);
        gt.GetField("_rowH", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(full, 0);
        list.Add(full);

        var st = new TextStyle(BuiltinFonts.UiSans, 14f, Vector4D<float>.One);
        TextRenderer.DrawLiteral(r, lib, cache, st, "A", new Vector2D<float>(0f, 12f));
        Assert.Equal(2, list.Count);
        Assert.NotEmpty(r.Sprites);
    }

}
