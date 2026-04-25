using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Updates the <see cref="HudFpsTag"/> BitmapText with a moving average of the present rate.</summary>
public sealed class FpsDisplaySystem : ISystem, ILateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<HudFpsTag, Transform, BitmapText>();

    private readonly GameHostServices _host;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    public FpsDisplaySystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query) => _ = (world, query);

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        var r = _host.Renderer;
        if (r is null)
            return;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        var label = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
        var fb = ModLayoutViewport.VirtualSizeForPresentation(r);
        foreach (var chunk in query)
        {
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var t = ref chunk.Column<Transform>()[i];
                t.LocalPosition = new Vector2D<float>(fb.X - 120f, fb.Y - 26f);
                t.WorldPosition = t.LocalPosition;
                ref var bt = ref chunk.Column<BitmapText>()[i];
                bt.Visible = true;
                bt.Content = label;
            }
        }
    }
}
