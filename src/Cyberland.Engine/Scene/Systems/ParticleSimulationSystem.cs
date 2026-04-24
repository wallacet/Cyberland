using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: integrates per-emitter CPU particles in-place on <see cref="ParticleEmitter"/> runtime SoA arrays.
/// </summary>
public sealed class ParticleSimulationSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ParticleEmitter>();

    /// <summary>Creates the system.</summary>
    public ParticleSimulationSystem() { }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref var emitter = ref chunk.Column<ParticleEmitter>()[i];
                SimulateEmitter(ref emitter, fixedDeltaSeconds);
            });
        }
    }

    private static void SimulateEmitter(ref ParticleEmitter e, float deltaSeconds)
    {
        if (!e.Active || e.MaxParticles <= 0 || e.ParticleLifeSeconds <= 0f)
            return;

        InitEmitter(ref e);

        e.SpawnAccumulator += deltaSeconds;
        while (e.SpawnAccumulator >= e.SpawnIntervalSeconds && e.RuntimeCount < e.MaxParticles &&
               e.SpawnIntervalSeconds > 0f)
        {
            e.SpawnAccumulator -= e.SpawnIntervalSeconds;
            var idx = e.RuntimeCount;
            e.RuntimePx[idx] = 0f;
            e.RuntimePy[idx] = 0f;
            e.RuntimeVx[idx] = e.EmissionVelocity.X;
            e.RuntimeVy[idx] = e.EmissionVelocity.Y;
            e.RuntimeLife[idx] = e.ParticleLifeSeconds;
            e.RuntimeCount++;
        }

        var g = e.GravityY;
        for (var p = 0; p < e.RuntimeCount;)
        {
            e.RuntimeLife[p] -= deltaSeconds;
            if (e.RuntimeLife[p] <= 0f)
            {
                var last = e.RuntimeCount - 1;
                if (p != last)
                {
                    e.RuntimePx[p] = e.RuntimePx[last];
                    e.RuntimePy[p] = e.RuntimePy[last];
                    e.RuntimeVx[p] = e.RuntimeVx[last];
                    e.RuntimeVy[p] = e.RuntimeVy[last];
                    e.RuntimeLife[p] = e.RuntimeLife[last];
                }

                e.RuntimeCount--;
                continue;
            }

            e.RuntimeVy[p] += g * deltaSeconds;
            e.RuntimePx[p] += e.RuntimeVx[p] * deltaSeconds;
            e.RuntimePy[p] += e.RuntimeVy[p] * deltaSeconds;
            p++;
        }
    }

    private static void InitEmitter(ref ParticleEmitter e)
    {
        var maxParticles = e.MaxParticles;
        if (e.RuntimePx is not null && e.RuntimePx.Length >= maxParticles)
            return;

        e.RuntimePx = new float[maxParticles];
        e.RuntimePy = new float[maxParticles];
        e.RuntimeVx = new float[maxParticles];
        e.RuntimeVy = new float[maxParticles];
        e.RuntimeLife = new float[maxParticles];
        e.RuntimeCount = 0;
    }
}
