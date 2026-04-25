using Cyberland.Demo.MouseChase.Components;
using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class PlayerMovementSystem : IParallelSystem, IParallelFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Transform>();

    private World _world = null!;
    private EntityId _controlEntity;
    private EntityId _stateEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
        _controlEntity = world.QueryChunks(SystemQuerySpec.All<ControlState>())
            .RequireSingleEntityWith<ControlState>("control state");
        _stateEntity = world.QueryChunks(SystemQuerySpec.All<GameState>())
            .RequireSingleEntityWith<GameState>("game state");
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        ref readonly var control = ref _world.Get<ControlState>(_controlEntity);
        ref readonly var state = ref _world.Get<GameState>(_stateEntity);
        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;
        var mouseWorld = control.MouseWorld;
        var speed = control.PrimaryPressed ? 420f : 300f;

        foreach (var chunk in query)
        {
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var transforms = chunk.Column<Transform>();
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    ref var playerTransform = ref transforms[i];
                    var toMouse = mouseWorld - playerTransform.WorldPosition;
                    var len = MathF.Sqrt(toMouse.X * toMouse.X + toMouse.Y * toMouse.Y);
                    if (len <= 1f)
                        continue;

                    var dir = toMouse / len;
                    playerTransform.WorldPosition += dir * speed * fixedDeltaSeconds;
                }
            });
        }
    }
}
