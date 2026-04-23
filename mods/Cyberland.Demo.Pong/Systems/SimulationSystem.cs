using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Fixed-step Pong simulation on the session entity. Sequential: one <see cref="State"/> entity, no chunk parallelism.
/// </summary>
public sealed class SimulationSystem : ISystem, IFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly VisualIds _visuals;

    public SimulationSystem(GameHostServices host, EntityId session, VisualIds visuals)
    {
        _host = host;
        _session = session;
        _visuals = visuals;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        if (_host.Renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Pong.SimulationSystem startup failed", "Host.Renderer was null during OnStart.");
            throw new InvalidOperationException("Cyberland.Demo.Pong SimulationSystem requires a renderer.");
        }

        var triggers = world.Components<Trigger>();

        // Trigger shapes are static for this demo; only transforms/enabled state change at runtime.
        triggers.GetOrAdd(_visuals.LeftPad) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH)
        };
        triggers.GetOrAdd(_visuals.RightPad) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Rectangle,
            HalfExtents = new Vector2D<float>(Constants.PaddleHalfW, Constants.PaddleHalfH)
        };
        triggers.GetOrAdd(_visuals.Ball) = new Trigger
        {
            Enabled = false,
            Shape = TriggerShapeKind.Circle,
            Radius = Constants.BallR
        };
    }

    public void OnFixedUpdate(World world, ChunkQueryAll archetype, float fixedDeltaSeconds)
    {
        _ = archetype;
        var fb = _host.Renderer!.SwapchainPixelSize;
        ref var st = ref world.Components<State>().Get(_session);
        ref var ctl = ref world.Components<Control>().Get(_session);
        var margin = 32f;
        st.ArenaMinX = margin + Constants.PaddleHalfW + 8f;
        st.ArenaMaxX = fb.X - margin - Constants.PaddleHalfW - 8f;
        st.ArenaMinY = margin;
        st.ArenaMaxY = fb.Y - margin;
        st.Pulse += fixedDeltaSeconds * Constants.TitlePulseSpeed;
        SyncTriggerBodies(world, in st);
        if (ctl.StartMatch) StartMatch(ref st, fb);
        ctl.StartMatch = false;
        if (st.Phase == Phase.Playing) StepPlaying(world, ref st, fb, ctl.PaddleUp, ctl.PaddleDown, fixedDeltaSeconds);
    }

    private void SyncTriggerBodies(World world, in State st)
    {
        var transforms = world.Components<Transform>();
        var triggers = world.Components<Trigger>();

        ref var leftTransform = ref transforms.Get(_visuals.LeftPad);
        leftTransform.LocalPosition = new Vector2D<float>(st.ArenaMinX, st.LeftPaddleY);
        leftTransform.WorldPosition = leftTransform.LocalPosition;
        ref var leftTrigger = ref triggers.Get(_visuals.LeftPad);
        leftTrigger.Enabled = st.Phase == Phase.Playing;

        ref var rightTransform = ref transforms.Get(_visuals.RightPad);
        rightTransform.LocalPosition = new Vector2D<float>(st.ArenaMaxX, st.RightPaddleY);
        rightTransform.WorldPosition = rightTransform.LocalPosition;
        ref var rightTrigger = ref triggers.Get(_visuals.RightPad);
        rightTrigger.Enabled = st.Phase == Phase.Playing;

        ref var ballTransform = ref transforms.Get(_visuals.Ball);
        ballTransform.LocalPosition = st.BallPos;
        ballTransform.WorldPosition = st.BallPos;
        ref var ballTrigger = ref triggers.Get(_visuals.Ball);
        ballTrigger.Enabled = st.Phase == Phase.Playing;
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
    private void StepPlaying(World world, ref State st, Vector2D<int> fb, bool paddleUp, bool paddleDown, float dt)
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
        ApplyTriggerPaddleContacts(world, ref st);
        if (st.BallPos.X < st.ArenaMinX - 40f) { st.CpuPoints++; if (st.CpuPoints >= Constants.WinScore || st.PlayerPoints >= Constants.WinScore) st.Phase = Phase.GameOver; else ResetBall(ref st, fb, false); }
        else if (st.BallPos.X > st.ArenaMaxX + 40f) { st.PlayerPoints++; if (st.CpuPoints >= Constants.WinScore || st.PlayerPoints >= Constants.WinScore) st.Phase = Phase.GameOver; else ResetBall(ref st, fb, true); }
    }
    private void ApplyTriggerPaddleContacts(World world, ref State st)
    {
        if (!world.Components<TriggerEvents>().TryGet(_visuals.Ball, out var triggerEvents) || triggerEvents.Events is null) return;
        foreach (var ev in triggerEvents.Events)
        {
            if (ev.Kind != TriggerEventKind.OnTriggerEnter) continue;
            if (ev.Other == _visuals.LeftPad && st.BallVel.X < 0f)
            {
                st.BallPos.X = st.ArenaMinX + Constants.PaddleHalfW + Constants.BallR;
                st.BallVel.X = MathF.Abs(st.BallVel.X);
                st.BallVel.Y += ((st.BallPos.Y - st.LeftPaddleY) / Constants.PaddleHalfH) * Constants.PaddleEnglish;
                NormalizeBallSpeed(ref st);
            }
            else if (ev.Other == _visuals.RightPad && st.BallVel.X > 0f)
            {
                st.BallPos.X = st.ArenaMaxX - Constants.PaddleHalfW - Constants.BallR;
                st.BallVel.X = -MathF.Abs(st.BallVel.X);
                st.BallVel.Y += ((st.BallPos.Y - st.RightPaddleY) / Constants.PaddleHalfH) * Constants.PaddleEnglish;
                NormalizeBallSpeed(ref st);
            }
        }
    }
}
