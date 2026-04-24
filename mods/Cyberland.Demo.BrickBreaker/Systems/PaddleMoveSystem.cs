using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Applies player horizontal movement to the paddle. Sequential fixed (single paddle).</summary>
public sealed class PaddleMoveSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly EntityId _stateEntity;
    private readonly EntityId _controlEntity;
    private readonly EntityId _paddleEntity;
    private World _world;

    public PaddleMoveSystem(EntityId stateEntity, EntityId controlEntity, EntityId paddleEntity)
    {
        _stateEntity = stateEntity;
        _controlEntity = controlEntity;
        _paddleEntity = paddleEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        var world = _world;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        ref var control = ref world.Components<Control>().Get(_controlEntity);
        ref var paddleTransform = ref world.Components<Transform>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);

        var move = Constants.PaddleMoveSpeed * fixedDeltaSeconds;
        if (control.MoveLeft)
            paddleTransform.LocalPosition.X -= move;
        if (control.MoveRight)
            paddleTransform.LocalPosition.X += move;

        paddleTransform.LocalPosition.Y = game.PaddleY;
        paddleTransform.LocalPosition.X = Math.Clamp(
            paddleTransform.LocalPosition.X,
            game.ArenaMinX + paddleBody.HalfWidth,
            game.ArenaMaxX - paddleBody.HalfWidth);
        paddleTransform.WorldPosition = paddleTransform.LocalPosition;

    }
}
