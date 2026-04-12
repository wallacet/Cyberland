using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Per-emitter SoA buffers (position, velocity, life) keyed by <see cref="EntityId"/>; <see cref="Systems.ParticleSimulationSystem"/> writes, <see cref="Systems.ParticleRenderSystem"/> reads.
/// </summary>
/// <remarks>Uses concurrent dictionaries so parallel systems can simulate different emitters safely.</remarks>
public sealed class ParticleStore
{
    /// <summary>SoA buffers for one emitter; returned by <see cref="EnsureCapacity"/> after sizing.</summary>
    public sealed class Bucket
    {
        /// <summary>World X positions (live indices <see cref="Count"/>).</summary>
        public float[] Px = Array.Empty<float>();
        /// <summary>World Y positions.</summary>
        public float[] Py = Array.Empty<float>();
        /// <summary>Velocity X.</summary>
        public float[] Vx = Array.Empty<float>();
        /// <summary>Velocity Y.</summary>
        public float[] Vy = Array.Empty<float>();
        /// <summary>Seconds of life remaining.</summary>
        public float[] Life = Array.Empty<float>();
        /// <summary>Number of live particles in the SoA arrays.</summary>
        public int Count;
    }

    private readonly ConcurrentDictionary<EntityId, Bucket> _buckets = new();
    /// <summary>Allocates (or grows) SoA arrays to hold at least <paramref name="maxParticles"/> live particles for <paramref name="owner"/>.</summary>
    /// <returns>The same <see cref="Bucket"/> instance used for simulation (single dictionary lookup after <see cref="EnsureCapacity"/>).</returns>
    public Bucket EnsureCapacity(EntityId owner, int maxParticles)
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

        return b;
    }

    internal bool TryGetBucket(EntityId owner, out Bucket? bucket) =>
        _buckets.TryGetValue(owner, out bucket);

    /// <summary>Drops all simulation state for <paramref name="owner"/> (e.g. when destroying an emitter entity).</summary>
    public void ClearEmitter(EntityId owner) =>
        _buckets.TryRemove(owner, out _);
}
