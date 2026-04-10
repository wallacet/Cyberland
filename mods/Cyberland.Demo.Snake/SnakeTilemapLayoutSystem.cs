using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene2D;

namespace Cyberland.Demo.Snake;

/// <summary>Updates <see cref="Tilemap"/> from <see cref="SnakeSession"/> layout so the engine tilemap pass draws the playfield grid.</summary>
public sealed class SnakeTilemapLayoutSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;
    private readonly EntityId _arena;

    public SnakeTilemapLayoutSystem(GameHostServices host, SnakeSession session, EntityId arena)
    {
        _host = host;
        _session = session;
        _arena = arena;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        var s = _session;
        s.UpdateLayout(fb.X, fb.Y);

        ref var tm = ref world.Components<Tilemap>().Get(_arena);
        tm.TileWidth = s.Cell;
        tm.TileHeight = s.Cell;
        tm.OriginX = s.OriginX;
        tm.OriginY = s.OriginY;
        tm.AtlasAlbedoTextureId = r.WhiteTextureId;
        tm.Layer = (int)SpriteLayer.Background;
        tm.SortKey = 0f;
        tm.NonEmptyTileMinIndex = 1;
    }
}
