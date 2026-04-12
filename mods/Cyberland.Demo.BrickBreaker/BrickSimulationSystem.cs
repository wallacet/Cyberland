using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

public sealed class BrickSimulationSystem : ISystem, IFixedUpdate
{
    private readonly GameHostServices _host;
    private readonly BrickSession _session;
    private readonly EntityId _controlEntity;

    public BrickSimulationSystem(GameHostServices host, BrickSession session, EntityId controlEntity)
    {
        _host = host;
        _session = session;
        _controlEntity = controlEntity;
    }

    public void OnFixedUpdate(World world, float fixedDeltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        var s = _session;
        var margin = 40f;
        s.ArenaMinX = margin;
        s.ArenaMaxX = fb.X - margin;
        s.ArenaMinY = margin + 80f;
        s.ArenaMaxY = fb.Y - margin;
        s.PaddleY = s.ArenaMinY + 36f;
        s.BrickW = (s.ArenaMaxX - s.ArenaMinX) / BrickConstants.Cols;
        s.BrickH = 22f;
        s.BrickOriginX = s.ArenaMinX;
        s.BrickTopY = s.ArenaMaxY - 40f;

        ref var ctl = ref world.Components<BrickControl>().Get(_controlEntity);

        if (ctl.StartRound)
            StartRound(s, fb);

        var moveLeft = ctl.MoveLeft;
        var moveRight = ctl.MoveRight;
        var launch = ctl.LaunchBall;
        ctl = default;

        if (s.Phase == BrickPhase.Playing)
        {
            var move = 420f * fixedDeltaSeconds;
            if (moveLeft)
                s.PaddleCenterX -= move;
            if (moveRight)
                s.PaddleCenterX += move;

            s.PaddleCenterX = Math.Clamp(s.PaddleCenterX, s.ArenaMinX + s.PaddleHalfW, s.ArenaMaxX - s.PaddleHalfW);

            if (s.BallDocked)
                s.BallPos = new Vector2D<float>(s.PaddleCenterX, s.PaddleY + s.PaddleHalfH + BrickConstants.BallR);

            if (s.BallDocked && launch)
            {
                s.BallDocked = false;
                s.BallVel = new Vector2D<float>((Random.Shared.NextSingle() - 0.5f) * 0.5f, 1f);
                var len = MathF.Sqrt(s.BallVel.X * s.BallVel.X + s.BallVel.Y * s.BallVel.Y);
                s.BallVel *= BrickConstants.BallSpeed / len;
            }

            if (!s.BallDocked)
                StepBall(s, fixedDeltaSeconds);
        }
    }

    private static void StartRound(BrickSession s, Vector2D<int> fb)
    {
        s.Phase = BrickPhase.Playing;
        s.Lives = BrickConstants.StartingLives;
        s.Score = 0;
        for (var x = 0; x < BrickConstants.Cols; x++)
        for (var y = 0; y < BrickConstants.Rows; y++)
            s.Bricks[x, y] = true;

        s.PaddleCenterX = fb.X * 0.5f;
        s.BallDocked = true;
        s.BallPos = new Vector2D<float>(s.PaddleCenterX, s.PaddleY + s.PaddleHalfH + BrickConstants.BallR);
        s.BallVel = default;
    }

    private static void StepBall(BrickSession s, float deltaSeconds)
    {
        s.BallPos += s.BallVel * deltaSeconds;

        if (s.BallPos.X - BrickConstants.BallR < s.ArenaMinX)
        {
            s.BallPos.X = s.ArenaMinX + BrickConstants.BallR;
            s.BallVel.X *= -1f;
        }
        else if (s.BallPos.X + BrickConstants.BallR > s.ArenaMaxX)
        {
            s.BallPos.X = s.ArenaMaxX - BrickConstants.BallR;
            s.BallVel.X *= -1f;
        }

        if (s.BallPos.Y + BrickConstants.BallR > s.ArenaMaxY)
        {
            s.BallPos.Y = s.ArenaMaxY - BrickConstants.BallR;
            s.BallVel.Y *= -1f;
        }

        if (s.BallVel.Y < 0f &&
            s.BallPos.Y - BrickConstants.BallR <= s.PaddleY + s.PaddleHalfH &&
            s.BallPos.Y > s.PaddleY - s.PaddleHalfH &&
            Math.Abs(s.BallPos.X - s.PaddleCenterX) < s.PaddleHalfW + BrickConstants.BallR)
        {
            s.BallPos.Y = s.PaddleY + s.PaddleHalfH + BrickConstants.BallR;
            s.BallVel.Y *= -1f;
            var off = (s.BallPos.X - s.PaddleCenterX) / s.PaddleHalfW;
            s.BallVel.X += off * 140f;
            var len = MathF.Sqrt(s.BallVel.X * s.BallVel.X + s.BallVel.Y * s.BallVel.Y);
            if (len > 1e-3f)
                s.BallVel *= BrickConstants.BallSpeed / len;
        }

        var hitBrick = false;
        for (var cx = 0; cx < BrickConstants.Cols && !hitBrick; cx++)
        for (var cy = 0; cy < BrickConstants.Rows && !hitBrick; cy++)
        {
            if (!s.Bricks[cx, cy])
                continue;

            var bx = s.BrickOriginX + (cx + 0.5f) * s.BrickW;
            var by = s.BrickTopY - (cy + 0.5f) * s.BrickH;
            var hw = s.BrickW * 0.46f;
            var hh = s.BrickH * 0.45f;
            if (s.BallPos.X > bx - hw - BrickConstants.BallR && s.BallPos.X < bx + hw + BrickConstants.BallR &&
                s.BallPos.Y > by - hh - BrickConstants.BallR && s.BallPos.Y < by + hh + BrickConstants.BallR)
            {
                s.Bricks[cx, cy] = false;
                s.Score += BrickConstants.BrickPoints;
                hitBrick = true;
                s.BallVel.Y *= -1f;
            }
        }

        if (s.BallPos.Y < s.ArenaMinY - 80f)
        {
            s.Lives--;
            if (s.Lives <= 0)
                s.Phase = BrickPhase.GameOver;
            else
            {
                s.BallDocked = true;
                s.BallPos = new Vector2D<float>(s.PaddleCenterX, s.PaddleY + s.PaddleHalfH + BrickConstants.BallR);
                s.BallVel = default;
            }
        }

        var any = false;
        for (var x = 0; x < BrickConstants.Cols; x++)
        for (var y = 0; y < BrickConstants.Rows; y++)
        {
            if (s.Bricks[x, y])
                any = true;
        }

        if (!any)
            s.Phase = BrickPhase.GameOver;
    }
}
