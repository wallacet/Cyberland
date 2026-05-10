using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Paddle hits from prior-tick <see cref="TriggerEvents"/> on the ball; brick hits via grid circle/AABB (no brick triggers).
/// </summary>
/// <remarks>
/// Omitting brick <see cref="Trigger"/> volumes keeps <see cref="Cyberland.Engine.Scene.Systems.TriggerSystem"/> pair work to ball + paddle only.
/// </remarks>
public sealed class TriggerResolveSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<BallTag, Transform, Velocity>();

    private World _world = null!;
    private EntityId _stateEntity;
    private EntityId _paddleEntity;
    private EntityId[] _brickCells = null!;

    public void OnSingletonStart(in SingletonEntity ballRow)
    {
        _world = ballRow.World;
        _stateEntity = Session.RequireStateEntity(_world);
        _paddleEntity = _world.QueryChunks(SystemQuerySpec.All<Paddle>())
            .RequireSingleEntityWith<Paddle>("brick paddle");
        _brickCells = _world.Get<ArenaBrickGrid>(_stateEntity).CellEntities;
    }

    public void OnSingletonFixedUpdate(in SingletonEntity ballRow, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref _world.Get<GameState>(_stateEntity);
        if (game.Phase != Phase.Playing || game.BallDocked)
            return;

        var ballEntity = ballRow.Entity;
        ref var ballTransform = ref ballRow.Get<Transform>();
        ref var ballVel = ref ballRow.Get<Velocity>();
        ref readonly var paddleTransform = ref _world.Get<Transform>(_paddleEntity);
        ref var paddleBody = ref _world.Get<PaddleBody>(_paddleEntity);
        var ballPos = ballTransform.LocalPosition;

        if (_world.TryGet<TriggerEvents>(ballEntity, out var triggerEvents) &&
            triggerEvents.Events is not null)
        {
            foreach (var ev in triggerEvents.Events)
            {
                if (ev.Kind != TriggerEventKind.OnTriggerEnter)
                    continue;
                if (ev.Other != _paddleEntity || ballVel.Value.Y >= 0f)
                    continue;

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
                break;
            }
        }

        ResolveBrickHits(ref game, ballPos, ref ballTransform, ref ballVel);
    }

    private void ResolveBrickHits(
        ref GameState game,
        Vector2D<float> ballPos,
        ref Transform ballTransform,
        ref Velocity ballVel)
    {
        var w = _world;
        var velocityTouched = false;
        var bw = game.BrickW;
        var bh = game.BrickH;
        // ±2 covers one-step motion vs brick size without scanning the full grid each tick.
        var fx = (ballPos.X - game.BrickOriginX) / bw;
        var fy = (game.BrickTopY - ballPos.Y) / bh;
        var ix0 = (int)MathF.Floor(fx);
        var iy0 = (int)MathF.Floor(fy);

        for (var oy = -2; oy <= 2; oy++)
        {
            var iy = iy0 + oy;
            if ((uint)iy >= Constants.Rows)
                continue;

            for (var ox = -2; ox <= 2; ox++)
            {
                var ix = ix0 + ox;
                if ((uint)ix >= Constants.Cols)
                    continue;

                var cellEntity = _brickCells[ix + iy * Constants.Cols];
                ref var cellState = ref w.Get<ArenaCellState>(cellEntity);
                if (!cellState.Active)
                    continue;

                ref readonly var cell = ref w.Get<Cell>(cellEntity);
                GetCellAabb(in game, in cell, out var cbx, out var cby, out var hwx, out var hhy);
                if (!CircleIntersectsAlignedRect(in ballPos, Constants.BallR, cbx, cby, hwx, hhy))
                    continue;

                cellState.Active = false;
                game.ActiveBricks = Math.Max(0, game.ActiveBricks - 1);
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

        ballTransform.LocalPosition = ballPos;
    }

    private static bool CircleIntersectsAlignedRect(
        in Vector2D<float> center,
        float radius,
        float rectCx,
        float rectCy,
        float halfW,
        float halfH)
    {
        var nx = Math.Clamp(center.X, rectCx - halfW, rectCx + halfW);
        var ny = Math.Clamp(center.Y, rectCy - halfH, rectCy + halfH);
        var dx = center.X - nx;
        var dy = center.Y - ny;
        var r = Math.Max(radius, 0f);
        return dx * dx + dy * dy <= r * r;
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
        if (adx > ady)
            vel = new Vector2D<float>(-vel.X, vel.Y);
        else
            vel = new Vector2D<float>(vel.X, -vel.Y);
    }
}
