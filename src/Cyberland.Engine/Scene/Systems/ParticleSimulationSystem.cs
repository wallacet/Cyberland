using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: integrates simple CPU particles into <see cref="ParticleStore"/> for each <see cref="ParticleEmitter"/> entity.
/// </summary>
public sealed class ParticleSimulationSystem : IParallelSystem, IParallelFixedUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ParticleEmitter>();

    /// <param name="host">Uses <see cref="Hosting.GameHostServices.Particles"/> buffer store.</param>
    public ParticleSimulationSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelFixedUpdate(World world, ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        var store = _host.Particles;
        var ids = _host.ParticleEmitterIdsForFrame;
        ids.Clear();
        if (store is null)
            return;

        foreach (var view in query)
        {
            var ents = view.Entities;
            for (var i = 0; i < view.Count; i++)
                ids.Add(ents[i]);
        }

        if (ids.Count == 0)
            return;

        var emitters = world.Components<ParticleEmitter>();

        Parallel.For(0, ids.Count, parallelOptions, i =>
            SimulateEmitter(emitters, store, ids[i], fixedDeltaSeconds));
    }

    private static void SimulateEmitter(ComponentStore<ParticleEmitter> emitters, ParticleStore store, EntityId id, float deltaSeconds)
    {
        ref var e = ref emitters.Get(id);
        if (!e.Active || e.MaxParticles <= 0 || e.ParticleLifeSeconds <= 0f)
            return;

        var b = store.EnsureCapacity(id, e.MaxParticles);

        e.SpawnAccumulator += deltaSeconds;
        while (e.SpawnAccumulator >= e.SpawnIntervalSeconds && b.Count < e.MaxParticles &&
               e.SpawnIntervalSeconds > 0f)
        {
            e.SpawnAccumulator -= e.SpawnIntervalSeconds;
            var idx = b.Count;
            b.Px[idx] = 0f;
            b.Py[idx] = 0f;
            b.Vx[idx] = e.EmissionVelocity.X;
            b.Vy[idx] = e.EmissionVelocity.Y;
            b.Life[idx] = e.ParticleLifeSeconds;
            b.Count++;
        }

        var g = e.GravityY;
        for (var p = 0; p < b.Count;)
        {
            b.Life[p] -= deltaSeconds;
            if (b.Life[p] <= 0f)
            {
                var last = b.Count - 1;
                if (p != last)
                {
                    b.Px[p] = b.Px[last];
                    b.Py[p] = b.Py[last];
                    b.Vx[p] = b.Vx[last];
                    b.Vy[p] = b.Vy[last];
                    b.Life[p] = b.Life[last];
                }

                b.Count--;
                continue;
            }

            b.Vy[p] += g * deltaSeconds;
            b.Px[p] += b.Vx[p] * deltaSeconds;
            b.Py[p] += b.Vy[p] * deltaSeconds;
            p++;
        }
    }
}
