using Cyberland.Engine.Input;
using Moq;
using Silk.NET.Input;

namespace Cyberland.Engine.Tests;

public sealed class KeyBindingAndInputTests
{
    [Fact]
    public void InputAction_stores_id()
    {
        var a = new InputAction("jump");
        Assert.Equal("jump", a.Id);
    }

    [Fact]
    public void KeyBindingStore_set_tryget_isdown()
    {
        var k = new KeyBindingStore();
        k.Set("a", Key.Space);
        Assert.True(k.TryGet("a", out var key));
        Assert.Equal(Key.Space, key);
        Assert.False(k.TryGet("b", out _));

        var kb = new Mock<IKeyboard>(MockBehavior.Strict);
        kb.Setup(x => x.IsKeyPressed(Key.Space)).Returns(true);
        Assert.True(k.IsDown(kb.Object, "a"));
        kb.Verify(x => x.IsKeyPressed(Key.Space), Times.Once);
    }

    [Fact]
    public void KeyBindingStore_LoadDefaults_populates_move_actions()
    {
        var k = new KeyBindingStore();
        k.LoadDefaults();
        Assert.True(k.TryGet("move_up", out var up));
        Assert.Equal(Key.W, up);
    }

    [Fact]
    public async Task KeyBindingStore_LoadOrCreateUserFileAsync_creates_file_when_absent()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb keys " + Guid.NewGuid() + ".json");
        try
        {
            var k = new KeyBindingStore();
            await k.LoadOrCreateUserFileAsync(path);
            Assert.True(File.Exists(path));
            var k2 = new KeyBindingStore();
            await k2.LoadOrCreateUserFileAsync(path);
            Assert.True(k2.TryGet("menu", out _));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task KeyBindingStore_LoadOrCreateUserFileAsync_null_json_falls_back_to_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb keys null " + Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllTextAsync(path, "null");
            var k = new KeyBindingStore();
            await k.LoadOrCreateUserFileAsync(path);
            Assert.True(k.TryGet("move_left", out _));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task KeyBindingStore_save_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "cyb keys rt " + Guid.NewGuid() + ".json");
        try
        {
            var k = new KeyBindingStore();
            k.LoadDefaults();
            k.Set("menu", Key.Q);
            await k.SaveAsync(path);
            var k2 = new KeyBindingStore();
            await k2.LoadOrCreateUserFileAsync(path);
            Assert.True(k2.TryGet("menu", out var m));
            Assert.Equal(Key.Q, m);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
