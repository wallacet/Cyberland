using Cyberland.Demo.MouseChase.Components;
using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class RoundStateSystem : IParallelSystem, IParallelFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();

    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        foreach (var chunk in query)
        {
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var states = chunk.Column<GameState>();
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    ref var state = ref states[i];
                    if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
                        continue;

                    state.TimerSeconds -= fixedDeltaSeconds;
                    if (state.TimerSeconds <= 0f || state.Health <= 0f)
                    {
                        state.Phase = RoundPhase.Lost;
                        continue;
                    }

                    if (state.Phase == RoundPhase.Tutorial
                        && state.Score >= state.TargetScore
                        && state.EnterZoneSeen
                        && state.StayZoneSeen
                        && state.ExitZoneSeen)
                        state.Phase = RoundPhase.Playing;
                }
            });
        }
    }
}
