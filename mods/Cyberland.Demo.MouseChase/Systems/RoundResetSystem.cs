using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class RoundResetSystem : ISystem, IFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();


    private World _world = null!;
    private readonly Random _rng = new(424242);
    private EntityId _controlEntity;
    private EntityId _playerEntity;
    private EntityId _collectibleEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
        _controlEntity = world.QueryChunks(SystemQuerySpec.All<ControlState>())
            .RequireSingleEntityWith<ControlState>("control state");
        _playerEntity = world.QueryChunks(SystemQuerySpec.All<PlayerTag, Transform>())
            .RequireSingleEntityWith<PlayerTag>("player");
        _collectibleEntity = world.QueryChunks(SystemQuerySpec.All<CollectibleTag, Transform>())
            .RequireSingleEntityWith<CollectibleTag>("collectible");
    }

    public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref readonly var control = ref _world.Get<ControlState>(_controlEntity);
        if (!control.RestartPressed)
            return;

        var w = _world;
        foreach (var chunk in query)
        {
            var states = chunk.Column<GameState>();
            for (var i = 0; i < chunk.Count; i++)
            {
                MouseChaseRoundLogic.ResetState(ref states[i]);
                ref var playerTransform = ref w.Get<Transform>(_playerEntity);
                playerTransform.LocalPosition = new Vector2D<float>(260f, 360f);
                ref var collectibleTransform = ref w.Get<Transform>(_collectibleEntity);
                MouseChaseRoundLogic.RespawnCollectible(ref collectibleTransform, _rng);
            }
        }
    }
}
