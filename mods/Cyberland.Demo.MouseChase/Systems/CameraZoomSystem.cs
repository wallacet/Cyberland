using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>Zooms the single gameplay <see cref="Camera2D"/> from buffered wheel delta.</summary>
public sealed class CameraZoomSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Camera2D>();

    private readonly GameHostServices _host;

    public CameraZoomSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity camera)
    {
        _ = camera;
    }

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity camera, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        var zoomDelta = _host.Input.ConsumeAxisDelta("cyberland.demo.mousechase/zoom");
        if (MathF.Abs(zoomDelta) <= 0.001f)
            return;

        var step = zoomDelta > 0f ? 0.93f : 1.07f;
        ref var cam = ref camera.Get<Camera2D>();
        var width = Math.Clamp((int)(cam.ViewportSizeWorld.X * step), 880, 1600);
        var height = Math.Clamp((int)(cam.ViewportSizeWorld.Y * step), 500, 900);
        cam.ViewportSizeWorld = new Vector2D<int>(width, height);
    }
}
