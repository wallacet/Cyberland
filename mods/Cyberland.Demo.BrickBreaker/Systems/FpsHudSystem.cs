using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Late: FPS row in viewport space (same pattern as the HDR <c>FpsDisplaySystem</c>).</summary>
public sealed class FpsHudSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudFpsTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(Constants.FpsAverageWindowSeconds);
    /// <summary>Last rounded FPS written to <see cref="BitmapText.Content"/>; <see cref="float.NaN"/> until first numeric publish.</summary>
    private float _lastPublishedRounded = float.NaN;
    private bool _publishedDash;

    public FpsHudSystem(GameHostServices host) => _host = host;

    public void OnSingletonStart(in SingletonEntity fpsRow)
    {
        _ = fpsRow;
    }

    public void OnSingletonLateUpdate(in SingletonEntity fpsRow, float deltaSeconds)
    {
        var r = _host.Renderer;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        ref var t = ref fpsRow.Get<Transform>();
        t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var bt = ref fpsRow.Get<BitmapText>();
        bt.Visible = fb.X > 0 && fb.Y > 0;
        if (!bt.Visible)
        {
            _lastPublishedRounded = float.NaN;
            _publishedDash = false;
            return;
        }

        _fps.AddFrameDeltaSeconds(frame);
        if (_fps.TryGetAverageFps(out var f))
        {
            var rounded = MathF.Round(f);
            if (!float.IsNaN(_lastPublishedRounded) && rounded == _lastPublishedRounded)
                return;
            _lastPublishedRounded = rounded;
            _publishedDash = false;
            bt.Content = $"FPS {rounded}";
        }
        else
        {
            if (_publishedDash)
                return;
            _publishedDash = true;
            _lastPublishedRounded = float.NaN;
            bt.Content = Constants.FpsHudAwaitingLabel;
        }
    }
}
