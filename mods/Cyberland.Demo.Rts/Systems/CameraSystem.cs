using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Late: pending camera focus, smooth zoom, WASD + edge scroll pan, playfield clamp.</summary>
/// <remarks>
/// Operates on the singleton camera row (<see cref="CameraTag"/>). Panning adjusts <see cref="Transform.WorldPosition"/> in **world** space (+Y up);
/// zoom mutates <see cref="Camera2D.ViewportSizeWorld"/> via <see cref="CameraZoomState"/> targets so the virtual canvas grows/shrinks while staying clamped to authored playfield bounds.
/// </remarks>
public sealed class CameraSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<CameraTag, Transform, Camera2D, CameraZoomState>();

    private readonly GameHostServices _host;
    private EntityId _sessionEntity;

    public CameraSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity cameraRow)
    {
        _sessionEntity = cameraRow.World.RequireSingleEntityWith<SessionState>("RTS session");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity cameraRow, float deltaSeconds)
    {
        var input = _host.Input;
        var renderer = _host.Renderer;
        ref var cam2 = ref cameraRow.Get<Camera2D>();
        ref var tf = ref cameraRow.Get<Transform>();
        ref var zoom = ref cameraRow.Get<CameraZoomState>();

        ref var session = ref cameraRow.World.Get<SessionState>(_sessionEntity);
        if (session.PendingCameraFocus)
        {
            tf.WorldPosition = session.PendingCameraFocusWorld;
            CameraBounds.ClampCenter(ref tf, cam2.ViewportSizeWorld.X, cam2.ViewportSizeWorld.Y);
            session.PendingCameraFocus = false;
        }

        ApplyWheelToZoomTargets(ref zoom, input);

        var k = 1f - MathF.Exp(-Constants.ZoomSmoothingPerSecond * deltaSeconds);
        var cw = cam2.ViewportSizeWorld.X;
        var nw = cw + (zoom.TargetViewportWidth - cw) * k;
        cam2.ViewportSizeWorld = ViewportSizeFromWidth((int)MathF.Round(nw));

        var ax = input.ReadAxis("cyberland.demo.rts/pan_x");
        var ay = input.ReadAxis("cyberland.demo.rts/pan_y");
        var pan = new Vector2D<float>(ax * Constants.PanSpeedKeyboard, ay * Constants.PanSpeedKeyboard);

        var mx = input.MousePositionScreen.X;
        var my = input.MousePositionScreen.Y;
        var sw = renderer.SwapchainPixelSize.X;
        var sh = renderer.SwapchainPixelSize.Y;
        var margin = Constants.EdgeScrollMarginPx;
        float evx = 0f, evy = 0f;
        if (mx >= 0f && my >= 0f && sw > 0 && sh > 0)
        {
            if (mx < margin)
                evx = -1f;
            else if (mx > sw - margin)
                evx = 1f;
            if (my < margin)
                evy = 1f;
            else if (my > sh - margin)
                evy = -1f;
        }

        pan.X += evx * Constants.PanSpeedEdge;
        pan.Y += evy * Constants.PanSpeedEdge;

        var p = tf.WorldPosition;
        tf.WorldPosition = new Vector2D<float>(p.X + pan.X * deltaSeconds, p.Y + pan.Y * deltaSeconds);

        CameraBounds.ClampCenter(ref tf, cam2.ViewportSizeWorld.X, cam2.ViewportSizeWorld.Y);
    }

    /// <summary>Wheel deltas are often ±120 per notch; scale targets exponentially and clamp width, then lock 16:9 height.</summary>
    private static void ApplyWheelToZoomTargets(ref CameraZoomState zoom, IInputService input)
    {
        var zd = input.ConsumeAxisDelta("cyberland.demo.rts/zoom");
        if (MathF.Abs(zd) <= 0.001f)
            return;

        var notches = Math.Clamp(MathF.Abs(zd) / 120f, 0.25f, 8f);
        var per = zd > 0f ? MathF.Pow(0.94f, notches) : MathF.Pow(1.06f, notches);
        zoom.TargetViewportWidth *= per;
        zoom.TargetViewportWidth = Math.Clamp(
            zoom.TargetViewportWidth,
            Constants.ZoomViewportMinWidth,
            Constants.ZoomViewportMaxWidth);
        zoom.TargetViewportHeight = zoom.TargetViewportWidth * (9f / 16f);
    }

    private static Vector2D<int> ViewportSizeFromWidth(int width)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, (int)MathF.Round(w * (9f / 16f)));
        return new Vector2D<int>(w, h);
    }
}
