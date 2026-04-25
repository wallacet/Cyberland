using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Resolves paddle and brick hits from <see cref="TriggerEvents"/> on the ball. The engine
/// <see cref="Scene.Systems.TriggerSystem"/> fills these in fixed update <strong>before</strong> this mod’s systems; brick and
/// paddle <see cref="Transform"/>s are already updated for this substep by earlier systems in this mod’s fixed chain.
/// </summary>
public sealed class TriggerResolveSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly EntityId _stateEntity;
    private readonly EntityId _paddleEntity;
    private readonly EntityId _ballEntity;
    private World _world;

    public TriggerResolveSystem(EntityId stateEntity, EntityId paddleEntity, EntityId ballEntity)
    {
        _stateEntity = stateEntity;
        _paddleEntity = paddleEntity;
        _ballEntity = ballEntity;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        _ = fixedDeltaSeconds;
        var world = _world;
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
        var ballPos = ballTransform.LocalPosition;
        var velocityTouched = false;

        foreach (var ev in triggerEvents.Events)
        {
            if (ev.Kind != TriggerEventKind.OnTriggerEnter)
                continue;

            if (ev.Other == _paddleEntity && ballVel.Value.Y < 0f)
            {
                var bp = ballPos;
                bp.Y = game.PaddleY + paddleBody.HalfHeight + Constants.BallR;
                ballVel.Value.Y = MathF.Abs(ballVel.Value.Y);
                var off = (bp.X - paddleTransform.WorldPosition.X) / paddleBody.HalfWidth;
                ballVel.Value.X += off * Constants.PaddleEnglish;
                var len = MathF.Sqrt(ballVel.Value.X * ballVel.Value.X + ballVel.Value.Y * ballVel.Value.Y);
                if (len > 1e-3f)
                    ballVel.Value *= Constants.BallSpeed / len;
                ballTransform.LocalPosition = bp;
                ballTransform.WorldPosition = bp;
                ballPos = bp;
                continue;
            }

            if (!brickCellStore.TryGet(ev.Other, out var cell))
                continue;
            if (!brickStateStore.TryGet(ev.Other, out var brickState) || !brickState.Active)
                continue;

            GetBrickAabb(in game, in cell, out var cbx, out var cby, out var hwx, out var hhy);
            brickState.Active = false;
            brickStateStore.Get(ev.Other) = brickState;
            ref var tri = ref triggerStore.Get(ev.Other);
            tri.Enabled = false;
            game.Score += Constants.BrickPoints;
            if (!velocityTouched)
            {
                BrickBounceHeuristic(in ballPos, cbx, cby, hwx, hhy, ref ballVel.Value);
                var len2 = MathF.Sqrt(ballVel.Value.X * ballVel.Value.X + ballVel.Value.Y * ballVel.Value.Y);
                if (len2 > 1e-3f)
                    ballVel.Value *= Constants.BallSpeed / len2;
                velocityTouched = true;
            }
        }
    }

    private static void GetBrickAabb(in GameState g, in Cell cell, out float cx, out float cy, out float halfW, out float halfH)
    {
        halfW = g.BrickW * 0.5f;
        halfH = g.BrickH * 0.5f;
        cx = g.BrickOriginX + (cell.X + 0.5f) * g.BrickW;
        cy = g.BrickTopY - (cell.Y + 0.5f) * g.BrickH;
    }

    private static void BrickBounceHeuristic(
        in Vector2D<float> c,
        float aabbCx,
        float aabbCy,
        float hwx,
        float hhy,
        ref Vector2D<float> vel)
    {
        // Dominant local axis: shallower overlap → flip along that face (AABB in world +Y up).
        var dx = c.X - aabbCx;
        var dy = c.Y - aabbCy;
        var adx = MathF.Abs(dx) / (hwx + 1e-4f);
        var ady = MathF.Abs(dy) / (hhy + 1e-4f);
        if (adx > ady) vel = new Vector2D<float>(-vel.X, vel.Y);
        else
            vel = new Vector2D<float>(vel.X, -vel.Y);
    }
}

