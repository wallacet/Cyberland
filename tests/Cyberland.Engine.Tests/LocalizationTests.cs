using System.Globalization;
using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Localization;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void LocalizationManager_merge_object_and_get_tryget_clear()
    {
        var loc = new LocalizationManager();
        loc.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        var json = """{"a":"1","b":"2"}"""u8.ToArray();
        loc.MergeJson(json);
        Assert.Equal("1", loc.Get("a"));
        Assert.True(loc.TryGet("b", out var b));
        Assert.Equal("2", b);
        Assert.False(loc.TryGet("missing", out _));
        Assert.Equal("missing", loc.Get("missing"));
        loc.Clear();
        Assert.Equal("a", loc.Get("a"));
    }

    [Fact]
    public void LocalizationManager_MergeJson_skips_non_objects()
    {
        var loc = new LocalizationManager();
        using var doc = JsonDocument.Parse("[]");
        loc.MergeJson(doc.RootElement);
        Assert.Equal("k", loc.Get("k"));
    }

    [Fact]
    public async Task LocalizationBootstrap_skips_when_path_missing()
    {
        var loc = new LocalizationManager();
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        await LocalizationBootstrap.LoadAsync(loc, assets, "Locale/nope.json");
        Assert.Equal("x", loc.Get("x"));
    }

    [Fact]
    public async Task LocalizationBootstrap_merges_when_present()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb loc " + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Locale"));
        File.WriteAllText(Path.Combine(root, "Locale", "en.json"), """{"hello":"world"}""");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var loc = new LocalizationManager();
            await LocalizationBootstrap.LoadAsync(loc, assets, "Locale/en.json");
            Assert.Equal("world", loc.Get("hello"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
