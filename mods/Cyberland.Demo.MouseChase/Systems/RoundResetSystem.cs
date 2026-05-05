using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>Consumes restart input in fixed update and resets poses.</summary>
public sealed class RoundResetSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();

    private EntityId _playerEntity;
    private EntityId _collectibleEntity;
    private readonly GameHostServices _host;
    private readonly Random _rng = new(424242);

    public RoundResetSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        var world = stateRow.World;
        _playerEntity = world.QueryChunks(SystemQuerySpec.All<PlayerTag, Transform>())
            .RequireSingleEntityWith<PlayerTag>("player");
        _collectibleEntity = world.QueryChunks(SystemQuerySpec.All<CollectibleTag, Transform>())
            .RequireSingleEntityWith<CollectibleTag>("collectible");
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity stateRow, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var state = ref stateRow.Get<GameState>();
        if (state.Phase is not (RoundPhase.Won or RoundPhase.Lost))
            return;

        var input = _host.Input;
        if (!input.ConsumePressed("cyberland.demo.mousechase/restart") &&
            !input.ConsumePressed("cyberland.common/start"))
            return;

        MouseChaseRoundLogic.ResetState(ref state);

        var world = stateRow.World;
        ref var playerTransform = ref world.Get<Transform>(_playerEntity);
        playerTransform.LocalPosition = new Vector2D<float>(260f, 360f);
        ref var collectibleTransform = ref world.Get<Transform>(_collectibleEntity);
        MouseChaseRoundLogic.RespawnCollectible(ref collectibleTransform, _rng);
    }
}
