using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>Viewport FPS readout (moving average) in the bottom-right; independent of tutorial HUD lines.</summary>
public sealed class FpsOverlaySystem : ISystem, ILateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _fpsText;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);
    private World _world = null!;

    public FpsOverlaySystem(GameHostServices host, EntityId fpsText)
    {
        _host = host;
        _fpsText = fpsText;
    }

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = query;
        var r = _host.Renderer;
        if (r is null)
            return;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        ref var t = ref _world.Get<Transform>(_fpsText);
        t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
        ref var bt = ref _world.Get<BitmapText>(_fpsText);
        bt.Visible = true;
        bt.Content = label;
        bt.IsLocalizationKey = false;
        bt.Style = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f));
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
    }
}
