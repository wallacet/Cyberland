using System.Collections.Generic;
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

    private readonly EntityId _stateEntity;
    private readonly List<MultiComponentChunkView> _chunks = new();

    public WinLoseSystem(EntityId stateEntity) => _stateEntity = stateEntity;

    public void OnParallelFixedUpdate(World world, ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        _chunks.Clear();
        foreach (var chunk in query)
            _chunks.Add(chunk);

        var activeCount = 0;
        Parallel.ForEach(
            _chunks,
            parallelOptions,
            chunk =>
            {
                var states = chunk.Column<BrickState>(0);
                var local = 0;
                for (var i = 0; i < chunk.Count; i++)
                {
                    if (states[i].Active)
                        local++;
                }
                if (local != 0)
                    Interlocked.Add(ref activeCount, local);
            });

        if (activeCount > 0)
            return;
        game.Phase = Phase.GameOver;
    }
}
