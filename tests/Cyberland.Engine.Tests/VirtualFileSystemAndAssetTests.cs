using Cyberland.Engine.Assets;
using Cyberland.Engine.Rendering;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class VirtualFileSystemAndAssetTests
{
    [Fact]
    public void VirtualFileSystem_Mount_skips_missing_directories()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(Path.Combine(Path.GetTempPath(), "cyberland_nonexistent_" + Guid.NewGuid()));
        Assert.Empty(vfs.Roots);
    }

    [Fact]
    public void VirtualFileSystem_Mount_skips_consecutive_duplicate_full_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb vfs dup " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "note.txt"), "x");
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            vfs.Mount(root);
            Assert.Single(vfs.Roots);
            Assert.True(vfs.TryOpenRead("note.txt", out var stream));
            using (stream)
            using (var reader = new StreamReader(stream!))
                Assert.Equal("x", reader.ReadToEnd());
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void VirtualFileSystem_last_mount_wins_for_try_open()
    {
        var a = Path.Combine(Path.GetTempPath(), "cyb vfs a " + Guid.NewGuid());
        var b = Path.Combine(Path.GetTempPath(), "cyb vfs b " + Guid.NewGuid());
        Directory.CreateDirectory(a);
        Directory.CreateDirectory(b);
        try
        {
            File.WriteAllText(Path.Combine(a, "data.txt"), "A");
            File.WriteAllText(Path.Combine(b, "data.txt"), "B");
            var vfs = new VirtualFileSystem();
            vfs.Mount(a);
            vfs.Mount(b);
            Assert.True(vfs.TryOpenRead("data.txt", out var stream));
            using (stream)
            using (var reader = new StreamReader(stream!))
                Assert.Equal("B", reader.ReadToEnd());

            Assert.True(vfs.Exists("/data.txt"));
            vfs.BlockPath("data.txt");
            Assert.False(vfs.Exists("data.txt"));
            Assert.False(vfs.TryOpenRead("data.txt", out _));
            Assert.True(vfs.UnblockPath("data.txt"));
            Assert.False(vfs.UnblockPath("data.txt"));
            Assert.True(vfs.Exists("data.txt"));
            vfs.Clear();
            Assert.False(vfs.Exists("data.txt"));
        }
        finally
        {
            try
            {
                Directory.Delete(a, true);
            }
            catch
            {
                /* ignore */
            }

            try
            {
                Directory.Delete(b, true);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [Fact]
    public void VirtualFileSystem_BlockPath_ignores_whitespace_only()
    {
        var vfs = new VirtualFileSystem();
        vfs.BlockPath("   ");
        Assert.Empty(vfs.Roots);
    }

    [Fact]
    public async Task AssetManager_LoadBytesAsync_throws_when_missing()
    {
        var vfs = new VirtualFileSystem();
        var assets = new AssetManager(vfs);
        await Assert.ThrowsAsync<FileNotFoundException>(() => assets.LoadBytesAsync("nope.bin"));
    }

    [Fact]
    public void AssetManager_LoadBytes_throws_when_missing()
    {
        var assets = new AssetManager(new VirtualFileSystem());
        Assert.Throws<FileNotFoundException>(() => assets.LoadBytes("nope.bin"));
    }

    [Fact]
    public void AssetManager_LoadBytes_and_LoadTexture_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb asset sync " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using (var image = new Image<Rgba32>(1, 1))
            {
                image[0, 0] = new Rgba32(10, 20, 30, 255);
                image.SaveAsPng(Path.Combine(root, "1.png"));
            }

            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var bytes = assets.LoadBytes("1.png");
            Assert.True(bytes.Length > 0);

            var renderer = new RecordingRenderer();
            var id = assets.LoadTexture("1.png", renderer);
            Assert.NotEqual(TextureId.MaxValue, id);
            Assert.Equal(1, renderer.RegisterTextureRgbaCallCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AssetManager_TryLoadTexture_missing_returns_missing_id()
    {
        var assets = new AssetManager(new VirtualFileSystem());
        var renderer = new RecordingRenderer();
        var result = assets.TryLoadTexture("missing.png", renderer);
        Assert.Equal(TextureLoadStatus.NotFound, result.Status);
        Assert.Equal(renderer.MissingTextureId, result.Id);
    }

    [Fact]
    public void AssetManager_TryLoadTexture_bad_bytes_returns_decode_failed()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-try-tex-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "bad.png"), "not png");
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var renderer = new RecordingRenderer();
            var result = assets.TryLoadTexture("bad.png", renderer);
            Assert.Equal(TextureLoadStatus.DecodeFailed, result.Status);
            Assert.Equal(renderer.MissingTextureId, result.Id);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AssetManager_LoadTexture_throws_when_missing()
    {
        var assets = new AssetManager(new VirtualFileSystem());
        var renderer = new RecordingRenderer();
        Assert.Throws<FileNotFoundException>(() => assets.LoadTexture("missing.png", renderer));
    }

    [Fact]
    public void AssetManager_TryLoadTexture_gpu_registration_failure_returns_status()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb-gpu-fail-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        using (var img = new Image<Rgba32>(2, 2))
            img.SaveAsPng(Path.Combine(root, "ok.png"));
        try
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            var renderer = new RecordingRenderer { RegisterTextureRgbaOverride = TextureId.MaxValue };
            var result = assets.TryLoadTexture("ok.png", renderer);
            Assert.Equal(TextureLoadStatus.GpuRegistrationFailed, result.Status);
            Assert.Equal(renderer.MissingTextureId, result.Id);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AssetManager_LoadText_and_Json_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb asset " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "t.json"), """{"X":7}""");
            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            Assert.Equal("{\"X\":7}", await assets.LoadTextAsync("t.json"));
            var o = await assets.LoadJsonAsync<Payload>("t.json");
            Assert.Equal(7, o.X);

            await using var s = assets.OpenReadOrThrow("t.json");
            Assert.True(s.Length > 0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task AssetManager_LoadText_strips_utf8_bom()
    {
        var root = Path.Combine(Path.GetTempPath(), "cyb asset bom " + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var payload = """{"X":7}"""u8.ToArray();
            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var fileBytes = new byte[bom.Length + payload.Length];
            bom.CopyTo(fileBytes, 0);
            payload.CopyTo(fileBytes, bom.Length);
            await File.WriteAllBytesAsync(Path.Combine(root, "t.json"), fileBytes);

            var vfs = new VirtualFileSystem();
            vfs.Mount(root);
            var assets = new AssetManager(vfs);
            Assert.Equal("{\"X\":7}", await assets.LoadTextAsync("t.json"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class Payload
    {
        public int X { get; set; }
    }
}
