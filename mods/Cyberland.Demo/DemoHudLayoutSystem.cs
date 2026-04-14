using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Positions engine <see cref="BitmapText"/> HUD rows from the current swapchain size (gameplay-only layout).</summary>
public sealed class DemoHudLayoutSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly EntityId _titleRow;
    private readonly EntityId _hintRow;

    public DemoHudLayoutSystem(GameHostServices host, EntityId titleRow, EntityId hintRow)
    {
        _host = host;
        _titleRow = titleRow;
        _hintRow = hintRow;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        ref var p0 = ref world.Components<Position>().Get(_titleRow);
        p0.X = 24f;
        p0.Y = fb.Y - 36f;

        ref var p1 = ref world.Components<Position>().Get(_hintRow);
        p1.X = 24f;
        p1.Y = 48f;
    }
}
