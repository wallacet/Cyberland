using Cyberland.Demo.Rts.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Late: FPS text top-right in <see cref="BitmapText.HudDefaultCoordinateSpace"/> (engine presentation canvas).</summary>
public sealed class RtsFpsHudSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<RtsHudFpsTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    public RtsFpsHudSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity hud)
    {
        _ = hud;
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity hud, float deltaSeconds)
    {
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForHudLayout(_host);
        ref var t = ref hud.Get<Transform>();
        ref var bt = ref hud.Get<BitmapText>();
        bt.Visible = fb.X > 0 && fb.Y > 0;
        if (!bt.Visible)
            return;
        // Top-right padding in presentation pixels; +Y is down in presentation space.
        t.LocalPosition = new Vector2D<float>(fb.X - 112f, 14f);
        bt.Content = label;
    }
}
