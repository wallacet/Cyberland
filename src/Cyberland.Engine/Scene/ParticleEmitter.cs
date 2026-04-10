using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Authoring fields for a simple CPU particle stream; <see cref="Systems.ParticleSimulationSystem"/> integrates positions into <see cref="ParticleStore"/>.
/// </summary>
public struct ParticleEmitter
{
    /// <summary>Time bank toward the next spawn.</summary>
    public float SpawnAccumulator;
    /// <summary>Seconds between spawn attempts (inverse rate).</summary>
    public float SpawnIntervalSeconds;
    /// <summary>Cap on live particles for this emitter.</summary>
    public int MaxParticles;
    /// <summary>Seconds until a spawned particle expires.</summary>
    public float ParticleLifeSeconds;
    /// <summary>Initial velocity in world units per second (+Y up).</summary>
    public Vector2D<float> EmissionVelocity;
    /// <summary>Downward acceleration (world units/s²); negative Y is “down” in world space.</summary>
    public float GravityY;
    /// <summary>Texture slot for each billboard.</summary>
    public int AlbedoTextureId;
    /// <summary>Sprite layer for draws.</summary>
    public int Layer;
    /// <summary>Sort key for submission.</summary>
    public float SortKey;
    /// <summary>Half-extent of each particle quad in world units.</summary>
    public float HalfExtent;
    /// <summary>When false, spawning stops (existing particles can still age out).</summary>
    public bool Active;
}
