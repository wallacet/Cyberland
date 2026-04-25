using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Applies <see cref="ViewportAnchor2D"/> to <see cref="Transform"/> (and optional <see cref="Sprite"/> half
/// extents) using <see cref="Hosting.GameHostServices.Renderer"/>'s
/// <see cref="Rendering.IRenderer.ActiveCameraViewportSize"/>, so HUD anchoring tracks the camera's virtual
/// viewport rather than the physical window (letterbox bars never clip the UI).
/// </summary>
public sealed class ViewportAnchorSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private World _world = null!;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ViewportAnchor2D, Transform>();

    /// <summary>Creates the system.</summary>
    public ViewportAnchorSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        var viewport = _host.CameraRuntimeState.ViewportSizeWorld;
        if ((viewport.X <= 0 || viewport.Y <= 0) && _host.Renderer is not null)
            viewport = _host.Renderer.ActiveCameraViewportSize;
        if (viewport.X <= 0 || viewport.Y <= 0)
            return;

        var w = _world;
        foreach (var chunk in archetype)
        {
            var ents = chunk.Entities;
            var anchors = chunk.Column<ViewportAnchor2D>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var a = ref anchors[i];
                if (!a.Active)
                    continue;

                var e = ents[i];
                var p = a.ContentSpace == CoordinateSpace.ViewportSpace
                    ? ComputeScreen(viewport, a)
                    : ComputeWorld(viewport, a);
                ref var transform = ref transforms[i];
                transform.LocalPosition = p;

                if (a.SyncSpriteHalfExtentsToViewport && w.Has<Sprite>(e))
                {
                    ref var s = ref w.Get<Sprite>(e);
                    s.HalfExtents = new Vector2D<float>(viewport.X * 0.5f, viewport.Y * 0.5f);
                }
            }
        }
    }

    private static Vector2D<float> ComputeScreen(Vector2D<int> viewport, in ViewportAnchor2D a)
    {
        var w = viewport.X;
        var h = viewport.Y;
        var ox = a.OffsetX;
        var oy = a.OffsetY;
        return a.Anchor switch
        {
            ViewportAnchorPreset.TopLeft => new Vector2D<float>(ox, oy),
            ViewportAnchorPreset.TopRight => new Vector2D<float>(w - ox, oy),
            ViewportAnchorPreset.BottomLeft => new Vector2D<float>(ox, h - oy),
            ViewportAnchorPreset.BottomRight => new Vector2D<float>(w - ox, h - oy),
            ViewportAnchorPreset.Center => new Vector2D<float>(w * 0.5f + ox, h * 0.5f + oy),
            ViewportAnchorPreset.LeftCenter => new Vector2D<float>(ox, h * 0.5f),
            _ => new Vector2D<float>(ox, oy)
        };
    }

    private static Vector2D<float> ComputeWorld(Vector2D<int> viewport, in ViewportAnchor2D a)
    {
        var screen = ComputeScreen(viewport, a);
        return WorldViewportSpace.ViewportPixelToWorldCenter(screen, viewport);
    }
}
