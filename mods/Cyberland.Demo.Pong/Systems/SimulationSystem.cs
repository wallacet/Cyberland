using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Fixed-step Pong on the session row. Paddle hits use circle-vs-rectangle tests (triggers are not used for this sample).
/// </summary>
public sealed class SimulationSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<State, Control>();

    private readonly GameHostServices _host;
    private readonly VisualIds _visuals;

    /// <summary>Sprite entity ids come from <see cref="SceneSetup"/>; state/control are the singleton row.</summary>
    public SimulationSystem(GameHostServices host, VisualIds visuals)
    {
        _host = host;
        _visuals = visuals;
    }

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity sessionRow, float fixedDeltaSeconds)
    {
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        ref var st = ref sessionRow.Get<State>();
        ref var ctl = ref sessionRow.Get<Control>();
        var margin = 32f;
        st.ArenaMinX = margin + Constants.PaddleHalfW + 8f;
        st.ArenaMaxX = fb.X - margin - Constants.PaddleHalfW - 8f;
        st.ArenaMinY = margin;
        st.ArenaMaxY = fb.Y - margin;
        st.Pulse += fixedDeltaSeconds * Constants.TitlePulseSpeed;
        SyncPaddleAndBallTransforms(sessionRow.World, in st);
        if (ctl.StartMatch) StartMatch(ref st, fb);
        ctl.StartMatch = false;
        if (st.Phase == Phase.Playing) StepPlaying(ref st, fb, ctl.PaddleUp, ctl.PaddleDown, fixedDeltaSeconds);
    }

    private void SyncPaddleAndBallTransforms(World w, in State st)
    {
        ref var leftTransform = ref w.Get<Transform>(_visuals.LeftPad);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, st.LeftPaddleY);

        ref var rightTransform = ref w.Get<Transform>(_visuals.RightPad);
        rightTransform.LocalPosition = new Vector2D<float>(st.ArenaMaxX, st.RightPaddleY);

        ref var ballTransform = ref w.Get<Transform>(_visuals.Ball);
        ballTransform.LocalPosition = st.BallPos;
    }

    private static void StartMatch(ref State st, Vector2D<int> fb)
    {
        st.Phase = Phase.Playing; st.PlayerPoints = 0; st.CpuPoints = 0;
        ResetBall(ref st, fb, true);
        st.LeftPaddleY = fb.Y * 0.5f; st.RightPaddleY = st.LeftPaddleY; st.LeftPaddleVelY = 0f; st.RightPaddleVelY = 0f;
    }

    private static void ResetBall(ref State st, Vector2D<int> fb, bool playerServes)
    {
        _ = fb;
        st.BallPos = new Vector2D<float>((st.ArenaMinX + st.ArenaMaxX) * 0.5f, (st.ArenaMinY + st.ArenaMaxY) * 0.5f);
        var sx = playerServes ? 1f : -1f;
        st.BallVel = new Vector2D<float>(sx * Constants.BallSpeed * 0.85f, (Random.Shared.NextSingle() - 0.5f) * Constants.BallSpeed * 0.4f);
        NormalizeBallSpeed(ref st);
        st.ServeDelay = Constants.ServeDelaySeconds;
    }

    private static void NormalizeBallSpeed(ref State st)
    {
        var len = MathF.Sqrt(st.BallVel.X * st.BallVel.X + st.BallVel.Y * st.BallVel.Y);
        if (len > 1e-3f) st.BallVel *= Constants.BallSpeed / len;
    }

    private void StepPlaying(ref State st, Vector2D<int> fb, bool paddleUp, bool paddleDown, float dt)
    {
        if (st.ServeDelay > 0f) { st.ServeDelay -= dt; st.LeftPaddleVelY = 0f; st.RightPaddleVelY = 0f; return; }
        var prevLeft = st.LeftPaddleY;
        var prevRight = st.RightPaddleY;
        var move = Constants.PlayerPaddleSpeed * dt;
        if (paddleUp) st.LeftPaddleY += move;
        if (paddleDown) st.LeftPaddleY -= move;
        st.LeftPaddleY = Math.Clamp(st.LeftPaddleY, st.ArenaMinY + Constants.PaddleHalfH, st.ArenaMaxY - Constants.PaddleHalfH);
        var cpuSpeed = Constants.CpuPaddleSpeed * dt;
        var target = st.BallPos.Y;
        if (st.RightPaddleY < target) st.RightPaddleY += Math.Min(cpuSpeed, target - st.RightPaddleY);
        else if (st.RightPaddleY > target) st.RightPaddleY -= Math.Min(cpuSpeed, st.RightPaddleY - target);
        st.RightPaddleY = Math.Clamp(st.RightPaddleY, st.ArenaMinY + Constants.PaddleHalfH, st.ArenaMaxY - Constants.PaddleHalfH);
        st.LeftPaddleVelY = (st.LeftPaddleY - prevLeft) / dt;
        st.RightPaddleVelY = (st.RightPaddleY - prevRight) / dt;
        st.BallPos += st.BallVel * dt;
        if (st.BallPos.Y + Constants.BallR > st.ArenaMaxY) { st.BallPos.Y = st.ArenaMaxY - Constants.BallR; st.BallVel.Y *= -1f; }
        else if (st.BallPos.Y - Constants.BallR < st.ArenaMinY) { st.BallPos.Y = st.ArenaMinY + Constants.BallR; st.BallVel.Y *= -1f; }

        ResolvePaddleContacts(ref st, fb);
        if (st.BallPos.X < st.ArenaMinX - 40f) { st.CpuPoints++; if (st.CpuPoints >= Constants.WinScore || st.PlayerPoints >= Constants.WinScore) st.Phase = Phase.GameOver; else ResetBall(ref st, fb, false); }
        else if (st.BallPos.X > st.ArenaMaxX + 40f) { st.PlayerPoints++; if (st.CpuPoints >= Constants.WinScore || st.PlayerPoints >= Constants.WinScore) st.Phase = Phase.GameOver; else ResetBall(ref st, fb, true); }
    }

    private static void ResolvePaddleContacts(ref State st, Vector2D<int> fb)
    {
        _ = fb;
        var hwx = Constants.PaddleHalfW;
        var hhy = Constants.PaddleHalfH;
        var r = Constants.BallR;
        if (st.BallVel.X < 0f && CircleIntersectsAabb(in st.BallPos, r, st.ArenaMinX, st.LeftPaddleY, hwx, hhy))
        {
            st.BallPos.X = st.ArenaMinX + hwx + r;
            st.BallVel.X = MathF.Abs(st.BallVel.X);
            st.BallVel.Y += ((st.BallPos.Y - st.LeftPaddleY) / hhy) * Constants.PaddleEnglish;
            NormalizeBallSpeed(ref st);
            return;
        }

        if (st.BallVel.X > 0f && CircleIntersectsAabb(in st.BallPos, r, st.ArenaMaxX, st.RightPaddleY, hwx, hhy))
        {
            st.BallPos.X = st.ArenaMaxX - hwx - r;
            st.BallVel.X = -MathF.Abs(st.BallVel.X);
            st.BallVel.Y += ((st.BallPos.Y - st.RightPaddleY) / hhy) * Constants.PaddleEnglish;
            NormalizeBallSpeed(ref st);
        }
    }

    private static bool CircleIntersectsAabb(
        in Vector2D<float> c,
        float r,
        float centerX,
        float centerY,
        float hwx,
        float hhy)
    {
        var minX = centerX - hwx;
        var maxX = centerX + hwx;
        var minY = centerY - hhy;
        var maxY = centerY + hhy;
        var qx = Math.Clamp(c.X, minX, maxX);
        var qy = Math.Clamp(c.Y, minY, maxY);
        var dx = c.X - qx;
        var dy = c.Y - qy;
        return dx * dx + dy * dy < r * r;
    }
}
