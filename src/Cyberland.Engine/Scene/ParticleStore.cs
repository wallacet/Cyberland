using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Per-emitter SoA buffers (position, velocity, life) keyed by <see cref="EntityId"/>; <see cref="Systems.ParticleSimulationSystem"/> writes, <see cref="Systems.ParticleRenderSystem"/> reads.
/// </summary>
/// <remarks>Uses concurrent dictionaries so parallel systems can simulate different emitters safely.</remarks>
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
    /// <summary>Allocates (or grows) SoA arrays to hold at least <paramref name="maxParticles"/> live particles for <paramref name="owner"/>.</summary>
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

    /// <summary>Drops all simulation state for <paramref name="owner"/> (e.g. when destroying an emitter entity).</summary>
    public void ClearEmitter(EntityId owner) =>
        _buckets.TryRemove(owner, out _);
}
