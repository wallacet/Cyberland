using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Resizes the background <see cref="Tilemap"/> to match the session’s computed cell and positions the map transform.
/// Snake body is drawn with per-segment <see cref="Sprite"/>s in <see cref="VisualSyncSystem"/>; tile indices are not
/// used for live gameplay in this sample.
/// </summary>
public sealed class TilemapLayoutSystem : ISystem, ILateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap>();

    private readonly GameHostServices _host;
    private readonly EntityId _sessionEntity;
    private readonly EntityId _arena;
    private World _world;
    public TilemapLayoutSystem(GameHostServices host, EntityId sessionEntity, EntityId arena)
    {
        _host = host;
        _sessionEntity = sessionEntity;
        _arena = arena;
    }
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _world = world;
        _ = archetype;
        var renderer = _host.RendererRequired;

        ref var tilemap = ref world.Components<Tilemap>().Get(_arena);
        tilemap.AtlasAlbedoTextureId = renderer.WhiteTextureId;
        tilemap.Layer = (int)SpriteLayer.Background;
        tilemap.SortKey = 0f;
        tilemap.NonEmptyTileMinIndex = 1;
    }
    public void OnLateUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        var world = _world;
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        if (fb.X <= 0 || fb.Y <= 0) return;
        ref var session = ref world.Components<Session>().Get(_sessionEntity);
        session.UpdateLayout(fb.X, fb.Y);
        ref var tilemap = ref world.Components<Tilemap>().Get(_arena);
        tilemap.TileWidth = session.Cell;
        tilemap.TileHeight = session.Cell;

        ref var tf = ref world.Components<Transform>().Get(_arena);
        tf.LocalPosition = WorldViewportSpace.ViewportPixelToWorldCenter(
            new Vector2D<float>(session.OriginX, session.OriginY),
            fb);
        tf.LocalRotationRadians = 0f;
        tf.LocalScale = new Vector2D<float>(1f, 1f);
    }
}
