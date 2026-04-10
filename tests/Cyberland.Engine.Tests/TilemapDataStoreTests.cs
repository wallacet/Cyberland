using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class TilemapDataStoreTests
{
    [Fact]
    public void Register_and_TryGet_round_trip()
    {
        var s = new TilemapDataStore();
        var id = new EntityId(5);
        var tiles = new[] { 1, 0, 2, 3 };
        s.Register(id, tiles, 2, 2);
        Assert.True(s.TryGet(id, out var mem, out var c, out var r));
        Assert.Equal(2, c);
        Assert.Equal(2, r);
        Assert.Equal(4, mem.Length);
        Assert.Equal(1, mem.Span[0]);
    }

    [Fact]
    public void Unregister_removes()
    {
        var s = new TilemapDataStore();
        var id = new EntityId(9);
        s.Register(id, new[] { 1 }, 1, 1);
        s.Unregister(id);
        Assert.False(s.TryGet(id, out _, out _, out _));
    }

    [Fact]
    public void Register_throws_on_mismatched_length()
    {
        var s = new TilemapDataStore();
        Assert.Throws<ArgumentException>(() => s.Register(new EntityId(1), ReadOnlySpan<int>.Empty, 2, 2));
    }

    [Fact]
    public void Register_throws_on_non_positive_dimensions()
    {
        var s = new TilemapDataStore();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Register(new EntityId(1), new[] { 1 }, 0, 1));
    }
}
