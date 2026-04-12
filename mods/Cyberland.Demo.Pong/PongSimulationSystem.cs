using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>Arena layout, match flow, ball/paddle physics, scoring.</summary>
/// <remarks>All displacement uses the fixed-step <c>fixedDeltaSeconds</c> from <see cref="IFixedUpdate.OnFixedUpdate"/> — not
/// the variable render delta — so gameplay stays consistent across refresh rates and frame pacing modes.</remarks>
public sealed class PongSimulationSystem : ISystem, IFixedUpdate
{
    private readonly GameHostServices _host;
    private readonly EntityId _session;

    public PongSimulationSystem(GameHostServices host, EntityId session)
    {
        _host = host;
        _session = session;
    }

    public void OnFixedUpdate(World world, float fixedDeltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        ref var st = ref world.Components<PongState>().Get(_session);
        ref var ctl = ref world.Components<PongControl>().Get(_session);

        var margin = 32f;
        st.ArenaMinX = margin + PongConstants.PaddleHalfW + 8f;
        st.ArenaMaxX = fb.X - margin - PongConstants.PaddleHalfW - 8f;
        st.ArenaMinY = margin;
        st.ArenaMaxY = fb.Y - margin;

        st.Pulse += fixedDeltaSeconds * 3f;

        if (ctl.StartMatch)
            StartMatch(ref st, fb);
        ctl.StartMatch = false;

        var up = ctl.PaddleUp;
        var down = ctl.PaddleDown;

        if (st.Phase == PongPhase.Playing)
            StepPlaying(ref st, fb, up, down, fixedDeltaSeconds);
    }

    private static void StartMatch(ref PongState st, Vector2D<int> fb)
    {
        st.Phase = PongPhase.Playing;
        st.PlayerPoints = 0;
        st.CpuPoints = 0;
        ResetBall(ref st, fb, playerServes: true);
        st.LeftPaddleY = fb.Y * 0.5f;
        st.RightPaddleY = st.LeftPaddleY;
        st.LeftPaddleVelY = 0f;
        st.RightPaddleVelY = 0f;
    }

    private static void ResetBall(ref PongState st, Vector2D<int> fb, bool playerServes)
    {
        _ = fb;
        st.BallPos = new Vector2D<float>((st.ArenaMinX + st.ArenaMaxX) * 0.5f, (st.ArenaMinY + st.ArenaMaxY) * 0.5f);
        var sx = playerServes ? 1f : -1f;
        st.BallVel = new Vector2D<float>(sx * PongConstants.BallSpeed * 0.85f,
            (Random.Shared.NextSingle() - 0.5f) * PongConstants.BallSpeed * 0.4f);
        NormalizeBallSpeed(ref st);
        st.ServeDelay = PongConstants.ServeDelaySeconds;
    }

    private static void NormalizeBallSpeed(ref PongState st)
    {
        var len = MathF.Sqrt(st.BallVel.X * st.BallVel.X + st.BallVel.Y * st.BallVel.Y);
        if (len > 1e-3f)
            st.BallVel *= PongConstants.BallSpeed / len;
    }

    private void StepPlaying(ref PongState st, Vector2D<int> fb, bool paddleUp, bool paddleDown, float dt)
    {
        if (st.ServeDelay > 0f)
        {
            st.ServeDelay -= dt;
            st.LeftPaddleVelY = 0f;
            st.RightPaddleVelY = 0f;
            return;
        }

        var prevLeft = st.LeftPaddleY;
        var prevRight = st.RightPaddleY;

        var move = PongConstants.PlayerPaddleSpeed * dt;
        if (paddleUp)
            st.LeftPaddleY += move;
        if (paddleDown)
            st.LeftPaddleY -= move;

        st.LeftPaddleY = Math.Clamp(st.LeftPaddleY, st.ArenaMinY + PongConstants.PaddleHalfH,
            st.ArenaMaxY - PongConstants.PaddleHalfH);

        var cpuSpeed = PongConstants.CpuPaddleSpeed * dt;
        var target = st.BallPos.Y;
        if (st.RightPaddleY < target)
            st.RightPaddleY += Math.Min(cpuSpeed, target - st.RightPaddleY);
        else if (st.RightPaddleY > target)
            st.RightPaddleY -= Math.Min(cpuSpeed, st.RightPaddleY - target);
        st.RightPaddleY = Math.Clamp(st.RightPaddleY, st.ArenaMinY + PongConstants.PaddleHalfH,
            st.ArenaMaxY - PongConstants.PaddleHalfH);

        st.LeftPaddleVelY = (st.LeftPaddleY - prevLeft) / dt;
        st.RightPaddleVelY = (st.RightPaddleY - prevRight) / dt;

        st.BallPos += st.BallVel * dt;

        if (st.BallPos.Y + PongConstants.BallR > st.ArenaMaxY)
        {
            st.BallPos.Y = st.ArenaMaxY - PongConstants.BallR;
            st.BallVel.Y *= -1f;
        }
        else if (st.BallPos.Y - PongConstants.BallR < st.ArenaMinY)
        {
            st.BallPos.Y = st.ArenaMinY + PongConstants.BallR;
            st.BallVel.Y *= -1f;
        }

        var leftX = st.ArenaMinX;
        var rightX = st.ArenaMaxX;
        if (st.BallVel.X < 0f &&
            st.BallPos.X - PongConstants.BallR < leftX + PongConstants.PaddleHalfW &&
            st.BallPos.X > leftX - PongConstants.PaddleHalfW &&
            Math.Abs(st.BallPos.Y - st.LeftPaddleY) < PongConstants.PaddleHalfH + PongConstants.BallR)
        {
            st.BallPos.X = leftX + PongConstants.PaddleHalfW + PongConstants.BallR;
            st.BallVel.X *= -1f;
            var off = (st.BallPos.Y - st.LeftPaddleY) / PongConstants.PaddleHalfH;
            st.BallVel.Y += off * PongConstants.PaddleEnglish;
            NormalizeBallSpeed(ref st);
        }
        else if (st.BallVel.X > 0f &&
                 st.BallPos.X + PongConstants.BallR > rightX - PongConstants.PaddleHalfW &&
                 st.BallPos.X < rightX + PongConstants.PaddleHalfW &&
                 Math.Abs(st.BallPos.Y - st.RightPaddleY) < PongConstants.PaddleHalfH + PongConstants.BallR)
        {
            st.BallPos.X = rightX - PongConstants.PaddleHalfW - PongConstants.BallR;
            st.BallVel.X *= -1f;
            var off = (st.BallPos.Y - st.RightPaddleY) / PongConstants.PaddleHalfH;
            st.BallVel.Y += off * PongConstants.PaddleEnglish;
            NormalizeBallSpeed(ref st);
        }

        if (st.BallPos.X < st.ArenaMinX - 40f)
        {
            st.CpuPoints++;
            if (st.CpuPoints >= PongConstants.WinScore || st.PlayerPoints >= PongConstants.WinScore)
                st.Phase = PongPhase.GameOver;
            else
                ResetBall(ref st, fb, playerServes: false);
        }
        else if (st.BallPos.X > st.ArenaMaxX + 40f)
        {
            st.PlayerPoints++;
            if (st.CpuPoints >= PongConstants.WinScore || st.PlayerPoints >= PongConstants.WinScore)
                st.Phase = PongPhase.GameOver;
            else
                ResetBall(ref st, fb, playerServes: true);
        }
    }
}
