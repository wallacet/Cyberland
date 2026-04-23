using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Resolves paddle and brick collisions from trigger events. Sequential fixed (single ball event list).</summary>
public sealed class TriggerResolveSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly EntityId _stateEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;

    public TriggerResolveSystem(EntityId stateEntity, EntityId paddleEntity, EntityId ballEntity)
    {
        _stateEntity = stateEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
    }

    public void OnFixedUpdate(World world, ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        _ = fixedDeltaSeconds;
        ref var game = ref world.Components<GameState>().Get(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        if (!world.Components<TriggerEvents>().TryGet(_ballEntity, out var triggerEvents) || triggerEvents.Events is null)
            return;

        ref var ballTransform = ref world.Components<Transform>().Get(_ballEntity);
        ref var ballVel = ref world.Components<Velocity>().Get(_ballEntity);
        ref readonly var paddleTransform = ref world.Components<Transform>().Get(_paddleEntity);
        ref var paddleBody = ref world.Components<PaddleBody>().Get(_paddleEntity);
        var brickStateStore = world.Components<BrickState>();
        var brickCellStore = world.Components<Cell>();
        var triggerStore = world.Components<Trigger>();

        foreach (var ev in triggerEvents.Events)
        {
            if (ev.Kind != TriggerEventKind.OnTriggerEnter)
                continue;

            if (ev.Other == _paddleEntity && ballVel.Value.Y < 0f)
            {
                ballTransform.LocalPosition.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
                ballVel.Value.Y = MathF.Abs(ballVel.Value.Y);
                var off = (ballTransform.LocalPosition.X - paddleTransform.WorldPosition.X) / paddleBody.HalfWidth;
                ballVel.Value.X += off * Constants.PaddleEnglish;
                var len = MathF.Sqrt(ballVel.Value.X * ballVel.Value.X + ballVel.Value.Y * ballVel.Value.Y);
                if (len > 1e-3f)
                    ballVel.Value *= Constants.BallSpeed / len;
                ballTransform.WorldPosition = ballTransform.LocalPosition;
                continue;
            }

            if (!brickCellStore.TryGet(ev.Other, out _))
                continue;
            if (!brickStateStore.TryGet(ev.Other, out var brickState) || !brickState.Active)
                continue;

            brickState.Active = false;
            brickStateStore.Get(ev.Other) = brickState;
            ref var trigger = ref triggerStore.Get(ev.Other);
            trigger.Enabled = false;
            game.Score += Constants.BrickPoints;
            ballVel.Value.Y *= -1f;
            break;
        }
    }
}
