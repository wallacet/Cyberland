using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.Tests;

public sealed class UiDocumentRegistryTests
{
    [Fact]
    public void Register_replaces_existing_document_for_same_entity()
    {
        var registry = new UiDocumentRegistry();
        var entity = new World().CreateEntity();
        var first = new UiDocument();
        var second = new UiDocument();

        registry.Register(entity, first);
        registry.Register(entity, second);

        Assert.True(registry.TryGet(entity, out var resolved));
        Assert.Same(second, resolved);
    }

    [Fact]
    public void Unregister_returns_false_when_entity_is_unknown()
    {
        var registry = new UiDocumentRegistry();
        Assert.False(registry.Unregister(new World().CreateEntity()));
    }

    [Fact]
    public void TryGet_returns_false_after_unregister()
    {
        var registry = new UiDocumentRegistry();
        var entity = new World().CreateEntity();
        registry.Register(entity, new UiDocument());
        Assert.True(registry.Unregister(entity));
        Assert.False(registry.TryGet(entity, out _));
    }

    [Fact]
    public void Register_null_document_throws()
    {
        var registry = new UiDocumentRegistry();
        Assert.Throws<ArgumentNullException>(() => registry.Register(new World().CreateEntity(), null!));
    }

    [Fact]
    public void PruneToEntities_removes_documents_not_in_active_set()
    {
        var registry = new UiDocumentRegistry();
        var world = new World();
        var keep = world.CreateEntity();
        var drop = world.CreateEntity();
        registry.Register(keep, new UiDocument());
        registry.Register(drop, new UiDocument());

        var removed = registry.PruneToEntities([keep]);
        Assert.Equal(1, removed);
        Assert.True(registry.TryGet(keep, out _));
        Assert.False(registry.TryGet(drop, out _));
    }
}
