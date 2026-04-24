using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Authoring and runtime fields for a simple CPU particle stream; <see cref="Systems.ParticleSimulationSystem"/> integrates
/// SoA particle state in-place and <see cref="Systems.ParticleRenderSystem"/> draws it.
/// </summary>
/// <remarks>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists
/// (see <see cref="RequiresComponentAttribute{TRequired}"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct ParticleEmitter : IComponent
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
    public TextureId AlbedoTextureId;
    /// <summary>Sprite layer for draws.</summary>
    public int Layer;
    /// <summary>Sort key for submission.</summary>
    public float SortKey;
    /// <summary>Half-extent of each particle quad in world units.</summary>
    public float HalfExtent;
    /// <summary>When false, spawning stops (existing particles can still age out).</summary>
    public bool Active;

    /// <summary>World X positions for live particles (indices [0, <see cref="RuntimeCount"/>)).</summary>
    public float[] RuntimePx;
    /// <summary>World Y positions for live particles.</summary>
    public float[] RuntimePy;
    /// <summary>Velocity X for live particles.</summary>
    public float[] RuntimeVx;
    /// <summary>Velocity Y for live particles.</summary>
    public float[] RuntimeVy;
    /// <summary>Seconds of remaining life for live particles.</summary>
    public float[] RuntimeLife;
    /// <summary>Live particle count in runtime SoA arrays.</summary>
    public int RuntimeCount;
}
