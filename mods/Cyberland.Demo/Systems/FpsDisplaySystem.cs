using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Late update for the FPS HUD row: positions and text in <see cref="BitmapText.HudDefaultCoordinateSpace"/> after simulation/post work.
/// </summary>
/// <remarks>
/// Uses <see cref="GameHostServices.LastPresentDeltaSeconds"/> when present so the average tracks actual GPU/frame pacing;
/// falls back to <paramref name="deltaSeconds"/> only when the host has not recorded a present yet (cold start).
/// Uses <see cref="ModLayoutViewport.VirtualSizeForHudLayout"/> for placement (matches presentation MSDF letterboxing).
/// Registered as <see cref="ISingletonSystem"/> for the single HUD FPS entity (see **cyberland-mod-patterns-hdr**).
/// </remarks>
public sealed class FpsDisplaySystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudFpsTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    /// <summary>Creates the system.</summary>
    public FpsDisplaySystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity hud, float deltaSeconds)
    {
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForHudLayout(_host);
        ref var t = ref hud.Get<Transform>();
        // Bottom-right padding in presentation pixels; BitmapText uses BitmapText.HudDefaultCoordinateSpace on this entity.
        t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var bt = ref hud.Get<BitmapText>();
        bt.Visible = true;
        bt.Content = label;
    }
}
