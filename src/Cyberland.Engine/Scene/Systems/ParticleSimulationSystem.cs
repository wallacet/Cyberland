using System.Collections.Generic;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: integrates simple CPU particles into <see cref="ParticleStore"/> for each <see cref="ParticleEmitter"/> entity.
/// </summary>
public sealed class ParticleSimulationSystem : IParallelSystem
{
    private readonly GameHostServices _host;

    /// <param name="host">Uses <see cref="Hosting.GameHostServices.Particles"/> buffer store.</param>
    public ParticleSimulationSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        var store = _host.Particles;
        if (store is null)
            return;

        var ids = new List<EntityId>();
        foreach (var view in world.QueryChunks<ParticleEmitter>())
        {
            var ents = view.Entities;
            for (var i = 0; i < view.Count; i++)
                ids.Add(ents[i]);
        }

        if (ids.Count == 0)
            return;

        Parallel.ForEach(ids, parallelOptions, id =>
            SimulateEmitter(world, store, id, deltaSeconds));
    }

    private static void SimulateEmitter(World world, ParticleStore store, EntityId id, float deltaSeconds)
    {
        ref var e = ref world.Components<ParticleEmitter>().Get(id);
        if (!e.Active || e.MaxParticles <= 0 || e.ParticleLifeSeconds <= 0f)
            return;

        store.EnsureCapacity(id, e.MaxParticles);
        var b = store.GetBucketAfterEnsure(id);

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
