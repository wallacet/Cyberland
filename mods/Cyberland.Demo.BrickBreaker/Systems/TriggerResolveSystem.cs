using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Resolves paddle and brick hits from <see cref="TriggerEvents"/> on the ball.
/// </summary>
/// <remarks>
/// <see cref="Cyberland.Engine.Scene.Systems.TriggerSystem"/> fills events in fixed update before this mod’s chain.
/// <see cref="TriggerEvents"/> stays on the ball entity but is not part of <see cref="QuerySpec"/> so the archetype stays stable
/// before the engine attaches the buffer—resolved via <see cref="World.TryGet{T}"/> on the ball row (see **cyberland-ecs-world-access**).
/// </remarks>
public sealed class TriggerResolveSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BallTag, Transform, Velocity>();

    private World _world = null!;
    private EntityId _stateEntity;
    private EntityId _paddleEntity;

    public void OnSingletonStart(in SingletonEntity ballRow)
    {
        _world = ballRow.World;
        _stateEntity = Session.RequireStateEntity(_world);
        _paddleEntity = _world.QueryChunks(SystemQuerySpec.All<Paddle>())
            .RequireSingleEntityWith<Paddle>("brick paddle");
    }

    public void OnSingletonFixedUpdate(in SingletonEntity ballRow, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        var ballEntity = ballRow.Entity;
        if (!_world.TryGet<TriggerEvents>(ballEntity, out var triggerEvents) || triggerEvents.Events is null)
            return;

        ref var ballTransform = ref ballRow.Get<Transform>();
        ref var ballVel = ref ballRow.Get<Velocity>();
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        var w = _world;
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
                ballPos = bp;
                continue;
            }

            if (!w.TryGet<Cell>(ev.Other, out var cell))
                continue;
            if (!w.TryGet<ArenaCellState>(ev.Other, out var cellState) || !cellState.Active)
                continue;

            GetCellAabb(in game, in cell, out var cbx, out var cby, out var hwx, out var hhy);
            ref var brSt = ref w.Get<ArenaCellState>(ev.Other);
            brSt.Active = false;
            ref var tri = ref w.Get<Trigger>(ev.Other);
            tri.Enabled = false;
            game.Score += Constants.PointsPerBlock;
            if (!velocityTouched)
            {
                BlockBounceHeuristic(in ballPos, cbx, cby, hwx, hhy, ref ballVel.Value);
                var len2 = MathF.Sqrt(ballVel.Value.X * ballVel.Value.X + ballVel.Value.Y * ballVel.Value.Y);
                if (len2 > 1e-3f)
                    ballVel.Value *= Constants.BallSpeed / len2;
                velocityTouched = true;
            }
        }
    }

    private static void GetCellAabb(in GameState g, in Cell cell, out float cx, out float cy, out float halfW, out float halfH)
    {
        halfW = g.BrickW * 0.5f;
        halfH = g.BrickH * 0.5f;
        cx = g.BrickOriginX + (cell.X + 0.5f) * g.BrickW;
        cy = g.BrickTopY - (cell.Y + 0.5f) * g.BrickH;
    }

    private static void BlockBounceHeuristic(
        in Vector2D<float> c,
        float aabbCx,
        float aabbCy,
        float hwx,
        float hhy,
        ref Vector2D<float> vel)
    {
        var dx = c.X - aabbCx;
        var dy = c.Y - aabbCy;
        var adx = MathF.Abs(dx) / (hwx + 1e-4f);
        var ady = MathF.Abs(dy) / (hhy + 1e-4f);
        if (adx > ady) vel = new Vector2D<float>(-vel.X, vel.Y);
        else
            vel = new Vector2D<float>(vel.X, -vel.Y);
    }
}
