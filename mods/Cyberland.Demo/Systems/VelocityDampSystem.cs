using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using System.Collections.Concurrent;

namespace Cyberland.Demo;

/// <summary>
/// Parallel fixed update: gently scales down <see cref="Velocity"/> each tick so keyboard impulse doesn’t cruise forever.
/// </summary>
/// <remarks>
/// Demonstrates <see cref="IParallelSystem"/> + <see cref="Parallel.ForEach"/> over chunk row ranges with host-provided
/// <see cref="ParallelOptions"/> (see design-goals parallelism). Toggle off at runtime via F9 from <see cref="InputSystem"/>.
/// The damping factor is framed per-second so changing <c>fixedDeltaSeconds</c> keeps similar feel at different fixed-step counts.
/// </remarks>
public sealed class VelocityDampSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Velocity>();

    /// <inheritdoc />
    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        // ~0.999 per frame at 60Hz fixed step — exponential decay keeps damping stable if the scheduler cadence changes slightly.
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
