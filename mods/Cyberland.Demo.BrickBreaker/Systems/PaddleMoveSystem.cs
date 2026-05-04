using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Applies player horizontal movement to the paddle (singleton paddle row).</summary>
/// <remarks>
/// Cross-reads <see cref="Control"/> and <see cref="GameState"/> from tagged session entities (see **cyberland-mod-patterns-hdr**).
/// </remarks>
public sealed class PaddleMoveSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Paddle, PaddleBody, Transform>();

    private EntityId _stateEntity;
    private EntityId _controlEntity;

    public void OnSingletonStart(in SingletonEntity paddleRow)
    {
        var world = paddleRow.World;
        _stateEntity = Session.RequireStateEntity(world);
        _controlEntity = world.QueryChunks(SystemQuerySpec.All<ControlTag>())
            .RequireSingleEntityWith<ControlTag>("brick control");
    }

    public void OnSingletonFixedUpdate(in SingletonEntity paddleRow, float fixedDeltaSeconds)
    {
        var world = paddleRow.World;
        ref var game = ref world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing)
            return;

        ref var control = ref world.Get<Control>(_controlEntity);
        ref var paddleTransform = ref paddleRow.Get<Transform>();
        ref var paddleBody = ref paddleRow.Get<PaddleBody>();

        var paddlePos = paddleTransform.LocalPosition;
        var move = Constants.PaddleMoveSpeed * fixedDeltaSeconds;
        if (control.MoveLeft)
            paddlePos.X -= move;
        if (control.MoveRight)
            paddlePos.X += move;

        paddlePos.Y = game.PaddleY;
        paddlePos.X = Math.Clamp(
            paddlePos.X,
            game.ArenaMinX + paddleBody.HalfWidth,
            game.ArenaMaxX - paddleBody.HalfWidth);
        paddleTransform.LocalPosition = paddlePos;
    }
}
