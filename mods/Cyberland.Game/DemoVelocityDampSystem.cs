using System.Buffers;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Game;

/// <summary>
/// Demonstrates parallel iteration: <see cref="Parallel.For"/> cannot close over <see cref="Span{T}"/>,
/// so we rent a scratch buffer, fan out, then write back (real systems will use fixed chunks or SoA views).
/// </summary>
public sealed class DemoVelocityDampSystem : IParallelSystem
{
    public void OnParallelUpdate(World world, ParallelOptions parallelOptions)
    {
        var store = world.Components<Velocity>();
        var n = store.Count;
        if (n == 0)
            return;

        var span = store.AsSpan();
        var rented = ArrayPool<Velocity>.Shared.Rent(n);
        try
        {
            span.CopyTo(rented);
            Parallel.For(0, n, parallelOptions, i =>
            {
                rented[i].X *= 0.999f;
                rented[i].Y *= 0.999f;
            });
            rented.AsSpan(0, n).CopyTo(span);
        }
        finally
        {
            ArrayPool<Velocity>.Shared.Return(rented);
        }
    }
}
