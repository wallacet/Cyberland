using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Fixed-step Pong on the session entity. Paddle hits use circle-vs-rectangle tests so the step does not depend on
/// engine trigger events (triggers run before mod <see cref="IFixedUpdate"/>; see <c>cyberland.engine/trigger</c> ordering).
/// </summary>
public sealed class SimulationSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly VisualIds _visuals;
    private World _world = null!;

    public SimulationSystem(GameHostServices host, EntityId session, VisualIds visuals)
    {
        _host = host;
        _session = session;
        _visuals = visuals;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.SimulationSystem startup failed", "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong SimulationSystem requires a renderer.");
        }
    }

    public void OnFixedUpdate(ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        var world = _world;
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        ref var st = ref world.Components<State>().Get(_session);
        ref var ctl = ref world.Components<Control>().Get(_session);
        var margin = 32f;
        st.ArenaMinX = margin + Constants.PaddleHalfW + 8f;
        st.ArenaMaxX = fb.X - margin - Constants.PaddleHalfW - 8f;
        st.ArenaMinY = margin;
        st.ArenaMaxY = fb.Y - margin;
        st.Pulse += fixedDeltaSeconds * Constants.TitlePulseSpeed;
        SyncPaddleAndBallTransforms(world, in st);
        if (ctl.StartMatch) StartMatch(ref st, fb);
        ctl.StartMatch = false;
        if (st.Phase == Phase.Playing) StepPlaying(ref st, fb, ctl.PaddleUp, ctl.PaddleDown, fixedDeltaSeconds);
    }

    private void SyncPaddleAndBallTransforms(World world, in State st)
    {
        var transforms = world.Components<Transform>();

        ref var leftTransform = ref transforms.Get(_visuals.LeftPad);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, st.LeftPaddleY);
        leftTransform.WorldPosition = leftTransform.LocalPosition;

        ref var rightTransform = ref transforms.Get(_visuals.RightPad);
        rightTransform.LocalPosition = new Vector2D<float>(st.ArenaMaxX, st.RightPaddleY);
        rightTransform.WorldPosition = rightTransform.LocalPosition;

        ref var ballTransform = ref transforms.Get(_visuals.Ball);
        ballTransform.LocalPosition = st.BallPos;
        ballTransform.WorldPosition = st.BallPos;
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
