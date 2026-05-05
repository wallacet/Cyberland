using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Clears the round when every block is inactive. Counts active cells in parallel over <see cref="ArenaCellState"/> chunks.
/// </summary>
/// <remarks>
/// Stays <see cref="IParallelSystem"/> — many brick rows justify <c>Parallel.For</c> over indices; not a single-row singleton driver.
/// </remarks>
public sealed class WinLoseSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ArenaCellState>();


    private World _world = null!;
    private EntityId _stateEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _stateEntity = Session.RequireStateEntity(world);
        _ = query;
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        var activeCount = 0;
        foreach (var chunk in query)
        {
            // Hoisting `chunk.Column<T>()` into a local would make it a ref local captured by the lambda (CS8175).
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                if (chunk.Column<ArenaCellState>()[i].Active)
                    Interlocked.Increment(ref activeCount);
            });
        }

        if (activeCount > 0)
            return;
        game.Phase = Phase.Won;
    }
}
