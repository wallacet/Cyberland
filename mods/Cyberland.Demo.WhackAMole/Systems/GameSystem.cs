using Cyberland.Demo.WhackAMole.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Scene;
using System.Numerics;
using Silk.NET.Maths;

namespace Cyberland.Demo.WhackAMole.Systems;

/// <summary>Handles click hit-testing, timed round flow, random target respawn, and HUD updates.</summary>
/// <remarks>
/// <para><b>Phases:</b> early for pointer hit tests against the moving target sprite; late for HUD string writes after sim state changes.</para>
/// <para>The fill <see cref="PointLightSource"/> is parented to the target at local origin so it follows the target via the engine transform hierarchy; viewport-rooted lights are mapped to world space for deferred lighting.</para>
/// </remarks>
public sealed class GameSystem : ISingletonSystem, ISingletonEarlyUpdate, ISingletonLateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<State>();
    public string SingletonLabel => "whack-a-mole state";

    private readonly GameHostServices _host;
    private readonly Random _rng = new(1729);
    private EntityId _background;
    private EntityId _target;
    private EntityId _targetFillLight;
    private readonly HudDocumentRefs _hud;

    public GameSystem(GameHostServices host, HudDocumentRefs hud)
    {
        _host = host;
        _hud = hud;
    }

    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        var world = stateRow.World;
        _background = world.RequireSingleEntityWith<BackgroundTag>("Whack-a-Mole background");
        _target = world.RequireSingleEntityWith<WhackAMoleTargetTag>("Whack-a-Mole target");
        _targetFillLight = world.RequireSingleEntityWith<TargetFillLightTag>("Whack-a-Mole target fill light");

        ref var state = ref stateRow.Get<State>();
        ResetRound(world, ref state);
    }

    public void OnSingletonEarlyUpdate(in SingletonEntity stateRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = stateRow.World;
        ref var state = ref stateRow.Get<State>();
        var input = _host.Input;

        if (input.IsDown("cyberland.common/quit"))
        {
            _host.Renderer.RequestClose?.Invoke();
            return;
        }

        if (state.Phase == WhackAMolePhase.GameOver)
        {
            if (input.HasAnyActionPressedThisFrame("cyberland.demo.whackamole/restart", "cyberland.common/start"))
                ResetRound(world, ref state);
            return;
        }

        if (!input.WasPressed("cyberland.demo.whackamole/hit"))
            return;

        var mouse = input.GetMousePosition(CoordinateSpace.ViewportSpace);
        if (!IsPointInsideTarget(world, mouse))
            return;

        state.Score++;
        if (!state.TimerStarted)
        {
            state.TimerStarted = true;
            state.Phase = WhackAMolePhase.Playing;
            state.TimeRemainingSeconds = 60f;
        }

        RespawnTarget(world);
    }

    public void OnSingletonLateUpdate(in SingletonEntity stateRow, float deltaSeconds)
    {
        var world = stateRow.World;
        ref var state = ref stateRow.Get<State>();

        if (state.TimerStarted && state.Phase == WhackAMolePhase.Playing)
        {
            state.TimeRemainingSeconds = MathF.Max(0f, state.TimeRemainingSeconds - deltaSeconds);
            if (state.TimeRemainingSeconds <= 0f)
            {
                state.Phase = WhackAMolePhase.GameOver;
                HideTarget(world);
            }
        }

        UpdateHud(world, in state);
    }

    private bool IsPointInsideTarget(World world, Vector2 mouseViewport)
    {
        ref var targetTransform = ref world.Get<Transform>(_target);
        ref var targetSprite = ref world.Get<Sprite>(_target);
        if (!targetSprite.Visible)
            return false;

        var minX = targetTransform.LocalPosition.X - targetSprite.HalfExtents.X;
        var maxX = targetTransform.LocalPosition.X + targetSprite.HalfExtents.X;
        var minY = targetTransform.LocalPosition.Y - targetSprite.HalfExtents.Y;
        var maxY = targetTransform.LocalPosition.Y + targetSprite.HalfExtents.Y;
        return mouseViewport.X >= minX && mouseViewport.X <= maxX && mouseViewport.Y >= minY && mouseViewport.Y <= maxY;
    }

    private void ResetRound(World world, ref State state)
    {
        state.Phase = WhackAMolePhase.Ready;
        state.Score = 0;
        state.TimeRemainingSeconds = 60f;
        state.TimerStarted = false;
        RespawnTarget(world);
    }

    private void RespawnTarget(World world)
    {
        ref var targetTransform = ref world.Get<Transform>(_target);
        ref var targetSprite = ref world.Get<Sprite>(_target);
        var viewport = ResolveViewportSize();
        var half = targetSprite.HalfExtents;

        var minX = half.X;
        var maxX = MathF.Max(minX, viewport.X - half.X);
        var minY = half.Y;
        var maxY = MathF.Max(minY, viewport.Y - half.Y);
        targetTransform.LocalPosition = new Vector2D<float>(
            NextFloat(minX, maxX),
            NextFloat(minY, maxY));
        targetSprite.Visible = true;
        targetSprite.ColorMultiply = new Vector4D<float>(
            NextFloat(0.35f, 1f),
            NextFloat(0.35f, 1f),
            NextFloat(0.35f, 1f),
            1f);
        SyncTargetFillLight(world, in targetSprite, active: true);
    }

    private void HideTarget(World world)
    {
        ref var targetSprite = ref world.Get<Sprite>(_target);
        targetSprite.Visible = false;
        ref var fill = ref world.Get<PointLightSource>(_targetFillLight);
        fill.Active = false;
    }

    private void SyncTargetFillLight(World world, in Sprite targetSprite, bool active)
    {
        ref var fill = ref world.Get<PointLightSource>(_targetFillLight);
        fill.Active = active;
        fill.Color = new Vector3D<float>(targetSprite.ColorMultiply.X, targetSprite.ColorMultiply.Y, targetSprite.ColorMultiply.Z);
    }

    private void SyncBackgroundToViewport(World world, in Vector2D<float> viewport)
    {
        ref var t = ref world.Get<Transform>(_background);
        t.LocalPosition = new Vector2D<float>(viewport.X * 0.5f, viewport.Y * 0.5f);
        ref var s = ref world.Get<Sprite>(_background);
        s.HalfExtents = new Vector2D<float>(viewport.X * 0.5f, viewport.Y * 0.5f);
    }

    private Vector2D<float> ResolveViewportSize()
    {
        var runtime = _host.CameraRuntimeState;
        if (runtime.Valid && runtime.ViewportSizeWorld.X > 0 && runtime.ViewportSizeWorld.Y > 0)
            return new Vector2D<float>(runtime.ViewportSizeWorld.X, runtime.ViewportSizeWorld.Y);

        var active = _host.Renderer.ActiveCameraViewportSize;
        if (active.X > 0 && active.Y > 0)
            return new Vector2D<float>(active.X, active.Y);

        return new Vector2D<float>(1280f, 720f);
    }

    private static string FormatTime(float secondsRemaining)
    {
        var total = Math.Max(0, (int)Math.Ceiling(secondsRemaining));
        var minutes = total / 60;
        var seconds = total % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private void UpdateHud(World world, in State state)
    {
        var viewport = ResolveViewportSize();
        SyncBackgroundToViewport(world, in viewport);

        _hud.Score.Text = $"Score: {state.Score}";
        _hud.Timer.Text = state.TimerStarted
            ? $"Time: {FormatTime(state.TimeRemainingSeconds)}"
            : "Time: 01:00 (click a square to start)";

        if (state.Phase == WhackAMolePhase.GameOver)
        {
            _hud.Overlay.Visible = true;
            _hud.Overlay.Text = $"Game Over  Score: {state.Score}  Press R to restart";
        }
        else
        {
            _hud.Overlay.Visible = false;
        }
    }

    private float NextFloat(float min, float max) => min + ((float)_rng.NextDouble() * (max - min));
}
