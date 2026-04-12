using Cyberland.Engine.Assets;

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

    private sealed class Payload
    {
        public int X { get; set; }
    }
}
