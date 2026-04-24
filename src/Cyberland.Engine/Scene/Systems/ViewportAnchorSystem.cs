using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Applies <see cref="ViewportAnchor2D"/> to <see cref="Transform"/> (and optional <see cref="Sprite"/> half extents) from <see cref="Hosting.GameHostServices.Renderer"/>'s swapchain size.
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
        var world = _world;
        var renderer = _host.Renderer!;

        var fb = renderer.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        var sprites = world.Components<Sprite>();
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
                var p = a.ContentSpace == CoordinateSpace.ScreenSpace
                    ? ComputeScreen(fb, a)
                    : ComputeWorld(fb, a);
                ref var transform = ref transforms[i];
                transform.LocalPosition = p;
                transform.WorldPosition = p;

                if (a.SyncSpriteHalfExtentsToViewport && sprites.Contains(e))
                {
                    ref var s = ref sprites.Get(e);
                    s.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
                }
            }
        }
    }

    private static Vector2D<float> ComputeScreen(Vector2D<int> fb, in ViewportAnchor2D a)
    {
        var w = fb.X;
        var h = fb.Y;
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

    private static Vector2D<float> ComputeWorld(Vector2D<int> fb, in ViewportAnchor2D a)
    {
        var screen = ComputeScreen(fb, a);
        return WorldScreenSpace.ScreenPixelToWorldCenter(screen, fb);
    }
}
