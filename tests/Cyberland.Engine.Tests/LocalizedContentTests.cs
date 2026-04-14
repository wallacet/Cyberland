using Cyberland.Engine.Assets;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.Tests;

public sealed class LocalizedContentTests
{
    [Fact]
    public void LocalizationCultureChains_en_only_merge_order()
    {
        var o = LocalizationCultureChains.StringTableMergeOrder("en");
        Assert.Single(o);
        Assert.Equal("en", o[0]);
    }

    [Fact]
    public void LocalizationCultureChains_deDE_includes_en_then_specifics()
    {
        var o = LocalizationCultureChains.StringTableMergeOrder("de-DE");
        Assert.Contains("en", o);
        Assert.Equal("en", o[0]);
        Assert.Contains("de", o);
        Assert.Equal("de-DE", o[^1]);
    }

    [Fact]
    public void LocalizationCultureChains_asset_probe_most_specific_first()
    {
        var p = LocalizationCultureChains.AssetResolutionCultureOrder("de-DE");
        Assert.Equal("de-DE", p[0]);
        Assert.Contains("en", p);
    }

    [Fact]
    public async Task LocalizedContent_MergeStringTableAsync_overlays_later_cultures()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-loc-merge-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Locale", "en"));
        Directory.CreateDirectory(Path.Combine(root, "Locale", "de"));
        File.WriteAllText(Path.Combine(root, "Locale", "en", "t.json"), """{"a":"en","b":"en"}""");
        File.WriteAllText(Path.Combine(root, "Locale", "de", "t.json"), """{"b":"de"}""");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var locMgr = new LocalizationManager();
            var lc = new LocalizedContent(locMgr, vfs, "de");
            await lc.MergeStringTableAsync("t.json");
            Assert.Equal("en", locMgr.Get("a"));
            Assert.Equal("de", locMgr.Get("b"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LocalizedContent_MergeStringTableAsync_skips_missing_english_then_loads_primary()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-loc-merge-deonly-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Locale", "de"));
        File.WriteAllText(Path.Combine(root, "Locale", "de", "only-de.json"), """{"k":"de"}""");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var locMgr = new LocalizationManager();
            var lc = new LocalizedContent(locMgr, vfs, "de");
            await lc.MergeStringTableAsync("only-de.json");
            Assert.Equal("de", locMgr.Get("k"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LocalizedContent_TryResolveLocalizedPath_prefers_specific_locale()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-loc-path-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Locale", "de", "Textures"));
        Directory.CreateDirectory(Path.Combine(root, "Textures"));
        File.WriteAllText(Path.Combine(root, "Locale", "de", "Textures", "x.png"), "de");
        File.WriteAllText(Path.Combine(root, "Textures", "x.png"), "base");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "de");
            Assert.Equal("Locale/de/Textures/x.png", lc.TryResolveLocalizedPath("Textures/x.png"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LanguageCommandLine_parses_equals_and_space_forms()
    {
        Assert.Equal("de", LanguageCommandLine.TryParseCulture(new[] { "--lang=de" }));
        Assert.Equal("fr-FR", LanguageCommandLine.TryParseCulture(new[] { "--lang", "fr-FR" }));
        Assert.Equal("en", LanguageCommandLine.TryParseCulture(new[] { "--lang=   " }));
        Assert.Null(LanguageCommandLine.TryParseCulture(Array.Empty<string>()));
        // Completes the for-loop without a match (covers loop close path in the state machine).
        Assert.Null(LanguageCommandLine.TryParseCulture(new[] { "not-a-lang-flag" }));
    }

    [Fact]
    public void LanguageSettingsFile_LoadPrimaryCulture_missing_file_returns_en()
    {
        Assert.Equal("en",
            LanguageSettingsFile.LoadPrimaryCulture(Path.Combine(Path.GetTempPath(), "missing-lang-" + Guid.NewGuid() + ".json")));
    }

    [Fact]
    public void LanguageSettingsFile_LoadPrimaryCulture_reads_saved_value()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb-lang-read-" + Guid.NewGuid() + ".json");
        try
        {
            LanguageSettingsFile.SavePrimaryCulture(path, "de");
            Assert.Equal("de", LanguageSettingsFile.LoadPrimaryCulture(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LanguageSettingsFile_LoadPrimaryCulture_empty_or_absent_primary_returns_en()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb-lang-empty-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, """{"primaryCulture":""}""");
            Assert.Equal("en", LanguageSettingsFile.LoadPrimaryCulture(path));
            File.WriteAllText(path, "{}");
            Assert.Equal("en", LanguageSettingsFile.LoadPrimaryCulture(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LanguageSettingsFile_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb-lang-" + Guid.NewGuid() + ".json");
        try
        {
            LanguageSettingsFile.SavePrimaryCulture(path, "de");
            Assert.Equal("de", LanguageSettingsFile.LoadPrimaryCulture(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LocalizedContent_PrimaryCultureName_and_GameHostServices_property()
    {
        var vfs = new VirtualFileSystem();
        var lc = new LocalizedContent(new LocalizationManager(), vfs, "fr-CA");
        Assert.Equal("fr-CA", lc.PrimaryCultureName);
        var host = new GameHostServices(new KeyBindingStore()) { LocalizedContent = lc };
        Assert.Same(lc, host.LocalizedContent);
    }

    [Fact]
    public async Task LocalizedContent_TryLoadLocalizedBytes_missing_returns_null()
    {
        var vfs = new VirtualFileSystem();
        var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
        Assert.Null(await lc.TryLoadLocalizedBytesAsync("Nope/missing.bin"));
    }

    [Fact]
    public void LocalizedContent_TryOpenLocalizedRead_blocked_path_returns_null()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-loc-block-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Textures"));
        File.WriteAllText(Path.Combine(root, "Textures", "a.png"), "x");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var resolved = lc.TryResolveLocalizedPath("Textures/a.png");
            Assert.NotNull(resolved);
            vfs.BlockPath(resolved!);
            Assert.Null(lc.TryOpenLocalizedRead("Textures/a.png"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LocalizedContent_TryLoadLocalizedBytes_and_TryOpenRead()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-loc-bytes-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Sounds"));
        File.WriteAllText(Path.Combine(root, "Sounds", "beep.raw"), "data");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
            var bytes = await lc.TryLoadLocalizedBytesAsync("Sounds/beep.raw");
            Assert.NotNull(bytes);
            Assert.Equal("data", System.Text.Encoding.UTF8.GetString(bytes!));

            await using var s = lc.TryOpenLocalizedRead("Sounds/beep.raw");
            Assert.NotNull(s);
            using var r = new StreamReader(s!);
            Assert.Equal("data", r.ReadToEnd());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void LanguagePreference_TryValidateCultureName_accepts_de()
    {
        Assert.Equal("de", LanguagePreference.TryValidateCultureName("de"));
    }

    [Fact]
    public void LanguagePreference_TryValidateCultureName_invalid_returns_en()
    {
        Assert.Equal("en", LanguagePreference.TryValidateCultureName("\0"));
    }

    [Fact]
    public void LocalizationCultureChains_NormalizeCultureName_empty_to_en()
    {
        Assert.Equal("en", LocalizationCultureChains.NormalizeCultureName("  "));
    }

    [Fact]
    public void LanguageCommandLine_lang_flag_alone_defaults_english()
    {
        Assert.Equal("en", LanguageCommandLine.TryParseCulture(new[] { "--lang" }));
    }

    [Fact]
    public void LanguageSettingsFile_LoadPrimaryCulture_corrupt_json_falls_back()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb-lang-bad-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ not json");
            Assert.Equal("en", LanguageSettingsFile.LoadPrimaryCulture(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LanguagePreference_cli_overrides_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb-langpref-" + Guid.NewGuid() + ".json");
        try
        {
            LanguageSettingsFile.SavePrimaryCulture(path, "fr");
            Assert.Equal("de", LanguagePreference.Resolve(new[] { "--lang", "de" }, path));
            Assert.Equal("fr", LanguagePreference.Resolve(ReadOnlySpan<string>.Empty, path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task LocalizedContent_MergeStringTableAsync_skips_whitespace_name()
    {
        var vfs = new VirtualFileSystem();
        var lc = new LocalizedContent(new LocalizationManager(), vfs, "en");
        await lc.MergeStringTableAsync("   ");
    }

    [Fact]
    public void LocalizedContent_TryResolveLocalizedPath_empty_returns_null()
    {
        var lc = new LocalizedContent(new LocalizationManager(), new VirtualFileSystem(), "en");
        Assert.Null(lc.TryResolveLocalizedPath("  "));
    }
}
