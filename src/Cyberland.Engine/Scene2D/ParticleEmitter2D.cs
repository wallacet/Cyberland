using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>CPU particle emitter; simulation state lives in <see cref="ParticleStore"/>.</summary>
public struct ParticleEmitter
{
    public float SpawnAccumulator;
    public float SpawnIntervalSeconds;
    public int MaxParticles;
    public float ParticleLifeSeconds;
    public Vector2D<float> EmissionVelocity;
    public float GravityY;
    public int AlbedoTextureId;
    public int Layer;
    public float SortKey;
    public float HalfExtent;
    public bool Active;
}
