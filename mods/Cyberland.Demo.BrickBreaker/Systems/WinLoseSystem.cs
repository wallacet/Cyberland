using System.Threading;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Clears the round when every brick is inactive. Counts active bricks in parallel over <see cref="BrickState"/> chunks.
/// </summary>
public sealed class WinLoseSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BrickState>();


    private World _world = null!;
    private readonly EntityId _stateEntity;
    public WinLoseSystem(EntityId stateEntity) => _stateEntity = stateEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
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
            Parallel.For(
                0,
                chunk.Count,
                parallelOptions,
                () => 0,
                (i, _, local) =>
                {
                    if (chunk.Column<BrickState>(0)[i].Active)
                        local++;
                    return local;
                },
                local =>
                {
                    if (local != 0)
                        Interlocked.Add(ref activeCount, local);
                });
        }

        if (activeCount > 0)
            return;
        game.Phase = Phase.Won;
    }
}
