using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.Snake;

/// <summary>Copies session layout into the arena <see cref="Tilemap"/>. Sequential late update.</summary>
public sealed class TilemapLayoutSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap>();

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _arena;
    public TilemapLayoutSystem(GameHostServices host, EntityId sessionEntity, EntityId arena)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _arena = arena;
    }
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        var renderer = _host.Renderer;
        if (renderer is null)
        {
            EngineDiagnostics.Report(EngineErrorSeverity.Major, "Cyberland.Demo.Snake.TilemapLayoutSystem", "Renderer was null during OnStart.");
            throw new InvalidOperationException("Renderer is required by TilemapLayoutSystem.");
        }

        ref var tilemap = ref world.Components<Tilemap>().Get(_arena);
        tilemap.AtlasAlbedoTextureId = renderer.WhiteTextureId;
        tilemap.Layer = (int)SpriteLayer.Background;
        tilemap.SortKey = 0f;
        tilemap.NonEmptyTileMinIndex = 1;
    }
    public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var renderer = _host.Renderer;
        if (renderer is null) return;
        var fb = renderer.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0) return;
        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        session.UpdateLayout(fb.X, fb.Y);
        ref var tilemap = ref world.Components<Tilemap>().Get(_arena);
        tilemap.TileWidth = session.Cell;
        tilemap.TileHeight = session.Cell;
        tilemap.OriginX = session.OriginX;
        tilemap.OriginY = session.OriginY;
    }
}
