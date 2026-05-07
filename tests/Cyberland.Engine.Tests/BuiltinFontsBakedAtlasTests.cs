using System.Reflection;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Engine.Tests;

public sealed class BuiltinFontsBakedAtlasTests
{
    [Fact]
    public void BuiltinFonts_EnumerateBakedAtlasResources_yields_readable_pages()
    {
        var count = 0;
        foreach (var (label, manifest, readPage) in BuiltinFonts.EnumerateBakedAtlasResources())
        {
            count++;
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.False(string.IsNullOrWhiteSpace(manifest.FamilyId));
            var page0 = readPage("page0.png");
            Assert.NotEmpty(page0);
            var fallback = readPage("not_page_named.png");
            Assert.NotEmpty(fallback);
            var ex = Assert.Throws<InvalidOperationException>(() => readPage("page999.png"));
            Assert.Contains(label, ex.Message, StringComparison.Ordinal);
        }

        Assert.True(count > 0, "expected at least one embedded baked atlas manifest");
    }

    [Fact]
    public void BuiltinFonts_EnumerateSingleAtlas_skips_when_manifest_resource_missing()
    {
        var m = typeof(BuiltinFonts).GetMethod(
            "EnumerateSingleAtlas",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        var asm = typeof(BuiltinFonts).Assembly;
        var en = m.Invoke(null, new object[] { asm, "l", "__missing_manifest__.json", "pfx" })!;
        Assert.Empty(ToList(en));
    }

    [Fact]
    public void BuiltinFonts_EnumerateSingleAtlas_skips_when_json_deserializes_to_null()
    {
        var m = typeof(BuiltinFonts).GetMethod(
            "EnumerateSingleAtlas",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(m);
        var asm = typeof(BuiltinFonts).Assembly;
        var en = m.Invoke(null, new object[] { asm, "l", "coverage-null-literal.json", "pfx" })!;
        Assert.Empty(ToList(en));
    }

    private static List<object?> ToList(object enumerable)
    {
        var list = new List<object?>();
        foreach (var item in (System.Collections.IEnumerable)enumerable)
            list.Add(item);
        return list;
    }
}
