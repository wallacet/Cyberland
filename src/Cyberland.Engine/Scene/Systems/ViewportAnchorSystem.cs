using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Applies <see cref="ViewportAnchor2D"/> to <see cref="Transform"/> (and optional <see cref="Sprite"/> half
/// extents) using the current camera runtime viewport, with a renderer fallback during early startup, so HUD
/// anchoring tracks the virtual camera canvas rather than the physical window (letterbox bars never clip the UI).
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
        var crs = _host.CameraRuntimeState;
        var runtimeVp = crs.Valid && crs.ViewportSizeWorld.X > 0 && crs.ViewportSizeWorld.Y > 0
            ? crs.ViewportSizeWorld
            : _host.Renderer.ActiveCameraViewportSize;
        if (runtimeVp.X <= 0 || runtimeVp.Y <= 0)
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
                var extent = a.ContentSpace == CoordinateSpace.PresentationViewportSpace && crs.Valid
                    ? CameraPresentationLayout.ResolvePresentationViewportSize(runtimeVp, crs.PresentationViewportSizeWorld)
                    : runtimeVp;
                var p = a.ContentSpace is CoordinateSpace.ViewportSpace or CoordinateSpace.PresentationViewportSpace
                    ? ComputeScreen(extent, a)
                    : ComputeWorld(runtimeVp, a);
                ref var transform = ref transforms[i];
                transform.LocalPosition = p;

                if (a.SyncSpriteHalfExtentsToViewport && w.Has<Sprite>(e))
                {
                    ref var s = ref w.Get<Sprite>(e);
                    var syncExtent = a.ContentSpace == CoordinateSpace.PresentationViewportSpace && crs.Valid
                        ? CameraPresentationLayout.ResolvePresentationViewportSize(runtimeVp, crs.PresentationViewportSizeWorld)
                        : runtimeVp;
                    s.HalfExtents = new Vector2D<float>(syncExtent.X * 0.5f, syncExtent.Y * 0.5f);
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
            ViewportAnchorPreset.LeftCenter => new Vector2D<float>(ox, h * 0.5f + oy),
            _ => new Vector2D<float>(ox, oy)
        };
    }

    private static Vector2D<float> ComputeWorld(Vector2D<int> viewport, in ViewportAnchor2D a)
    {
        var screen = ComputeScreen(viewport, a);
        return WorldViewportSpace.ViewportPixelToWorldCenter(screen, viewport);
    }
}
