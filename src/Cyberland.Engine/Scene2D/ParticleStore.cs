using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene2D;

/// <summary>SoA particle buffers per emitter entity (concurrent adds for parallel simulation).</summary>
public sealed class ParticleStore
{
    internal sealed class Bucket
    {
        public float[] Px = Array.Empty<float>();
        public float[] Py = Array.Empty<float>();
        public float[] Vx = Array.Empty<float>();
        public float[] Vy = Array.Empty<float>();
        public float[] Life = Array.Empty<float>();
        public int Count;
    }

    private readonly ConcurrentDictionary<EntityId, Bucket> _buckets = new();
    public void EnsureCapacity(EntityId owner, int maxParticles)
    {
        var b = _buckets.GetOrAdd(owner, static _ => new Bucket());

        if (b.Px.Length < maxParticles)
        {
            b.Px = new float[maxParticles];
            b.Py = new float[maxParticles];
            b.Vx = new float[maxParticles];
            b.Vy = new float[maxParticles];
            b.Life = new float[maxParticles];
        }
    }

    internal bool TryGetBucket(EntityId owner, out Bucket? bucket) =>
        _buckets.TryGetValue(owner, out bucket);

    internal Bucket GetBucketAfterEnsure(EntityId owner)
    {
        TryGetBucket(owner, out var b);
        return b!;
    }

    public void ClearEmitter(EntityId owner) =>
        _buckets.TryRemove(owner, out _);
}
