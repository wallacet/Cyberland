using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Scene;
using System.Collections.Concurrent;

namespace Cyberland.Demo;

/// <summary>
/// Parallel fixed update: scales every <see cref="Velocity"/> component by a per-tick factor using <see cref="Parallel.ForEach"/>
/// over matching chunks. This mod keeps <see cref="IParallelSystem"/> here only—other demo systems are sequential because they
/// touch singletons or submit lights without chunk work.
/// </summary>
public sealed class VelocityDampSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Velocity>();

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        var factor = MathF.Pow(0.999f, fixedDeltaSeconds * 60f);
        foreach (var chunk in query)
        {
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var velocity = chunk.Column<Velocity>(0);
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    ref var v = ref velocity[i];
                    v.X *= factor;
                    v.Y *= factor;
                }
            });
        }
    }
}
