using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>
/// Viewport FPS readout (moving average) in the bottom-right; singleton row is the entity tagged <see cref="FpsHudTag"/>.
/// </summary>
/// <remarks>Same ordering constraint as <see cref="TutorialHudSystem"/> — update <see cref="BitmapText"/> before <see cref="TextRenderSystem"/>.</remarks>
[RunBefore("cyberland.engine/text-render")]
public sealed class FpsOverlaySystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<FpsHudTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    /// <summary>Creates the overlay; no entity ids — the tagged HUD row is the singleton query.</summary>
    public FpsOverlaySystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity fpsRow, float deltaSeconds)
    {
        var r = _host.Renderer!;

        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        ref var t = ref fpsRow.Get<Transform>();
        t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var bt = ref fpsRow.Get<BitmapText>();
        bt.Visible = true;
        bt.Content = label;
        bt.IsLocalizationKey = false;
        bt.Style = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
    }
}
