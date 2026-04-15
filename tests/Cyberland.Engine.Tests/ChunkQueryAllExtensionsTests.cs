using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Tests;

public sealed class ChunkQueryAllExtensionsTests
{
    [Fact]
    public void RequireSingleEntityWith_throws_when_missing()
    {
        var w = new World();
        var q = w.QueryChunks(SystemQuerySpec.All<PlayerTag>());
        var ex = Assert.Throws<InvalidOperationException>(() => q.RequireSingleEntityWith<PlayerTag>("player"));
        Assert.Contains("Missing", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PlayerTag", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequireSingleEntityWith_throws_when_duplicate()
    {
        var w = new World();
        w.CreateEntity();
        w.Components<PlayerTag>().GetOrAdd(w.CreateEntity()) = default;
        w.Components<PlayerTag>().GetOrAdd(w.CreateEntity()) = default;
        var q = w.QueryChunks(SystemQuerySpec.All<PlayerTag>());
        var ex = Assert.Throws<InvalidOperationException>(() => q.RequireSingleEntityWith<PlayerTag>("player"));
        Assert.Contains("exactly one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequireSingleEntityWith_returns_entity_when_unique()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.Components<PlayerTag>().GetOrAdd(e) = default;
        var q = w.QueryChunks(SystemQuerySpec.All<PlayerTag>());
        Assert.Equal(e, q.RequireSingleEntityWith<PlayerTag>("player"));
    }

    [Fact]
    public void TryGetSingleEntityWith_false_when_empty_or_many()
    {
        var w = new World();
        Assert.False(w.QueryChunks(SystemQuerySpec.All<PlayerTag>()).TryGetSingleEntityWith<PlayerTag>(out _));

        w.Components<PlayerTag>().GetOrAdd(w.CreateEntity()) = default;
        w.Components<PlayerTag>().GetOrAdd(w.CreateEntity()) = default;
        Assert.False(w.QueryChunks(SystemQuerySpec.All<PlayerTag>()).TryGetSingleEntityWith<PlayerTag>(out _));
    }

    [Fact]
    public void TryGetSingleEntityWith_true_when_unique()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.Components<PlayerTag>().GetOrAdd(e) = default;
        Assert.True(w.QueryChunks(SystemQuerySpec.All<PlayerTag>()).TryGetSingleEntityWith<PlayerTag>(out var id));
        Assert.Equal(e, id);
    }

    [Fact]
    public void World_extensions_delegate_to_chunk_query()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.Components<PlayerTag>().GetOrAdd(e) = default;
        Assert.Equal(e, w.RequireSingleEntityWith<PlayerTag>("player"));
        Assert.True(w.TryGetSingleEntityWith<PlayerTag>(out var id));
        Assert.Equal(e, id);
    }

    private struct PlayerTag
    {
    }
}
