using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class ParticleStoreTests
{
    [Fact]
    public void EnsureCapacity_allocates_bucket()
    {
        var s = new ParticleStore();
        var id = new EntityId(3);
        s.EnsureCapacity(id, 4);
        Assert.True(s.TryGetBucket(id, out var b) && b is not null);
        Assert.Equal(4, b!.Px.Length);
    }

    [Fact]
    public void ClearEmitter_removes()
    {
        var s = new ParticleStore();
        var id = new EntityId(2);
        s.EnsureCapacity(id, 2);
        s.ClearEmitter(id);
        Assert.False(s.TryGetBucket(id, out _));
    }
}
