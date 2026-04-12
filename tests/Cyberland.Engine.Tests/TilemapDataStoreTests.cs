using System.Runtime.InteropServices;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

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
    public void Register_same_dimensions_reuses_tile_buffer()
    {
        var s = new TilemapDataStore();
        var id = new EntityId(5);
        s.Register(id, new[] { 1, 0, 2, 3 }, 2, 2);
        Assert.True(s.TryGet(id, out var first, out _, out _));
        s.Register(id, new[] { 9, 8, 7, 6 }, 2, 2);
        Assert.True(s.TryGet(id, out var second, out _, out _));
        Assert.True(MemoryMarshal.TryGetArray(first, out var seg1));
        Assert.True(MemoryMarshal.TryGetArray(second, out var seg2));
        Assert.Same(seg1.Array, seg2.Array);
        Assert.Equal(9, second.Span[0]);
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
