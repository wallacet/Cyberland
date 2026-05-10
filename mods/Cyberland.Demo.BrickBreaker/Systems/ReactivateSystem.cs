using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Parallel fixed: re-enables all blocks after <see cref="RoundStartSystem"/> sets <see cref="GameState.PendingReactivation"/>.
/// </summary>
public sealed class ReactivateSystem : IParallelSystem, IParallelFixedUpdate
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
        if (!game.PendingReactivation)
            return;

        game.PendingReactivation = false;
        game.ActiveBricks = Constants.Cols * Constants.Rows;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                var states = chunk.Column<ArenaCellState>();
                ref var bs = ref states[i];
                bs.Active = true;
            });
        }
    }
}
