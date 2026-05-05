using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Resizes the background <see cref="Tilemap"/> to match the session’s computed cell size and positions the map transform.
/// </summary>
/// <remarks>
/// Snake body uses per-cell <see cref="Sprite"/>s in <see cref="VisualSyncSystem"/>; tile indices are decorative background only.
/// </remarks>
public sealed class TilemapLayoutSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Tilemap, Transform>();

    private EntityId _sessionEntity;
    private readonly GameHostServices _host;

    /// <summary>Creates the tilemap layout pass.</summary>
    public TilemapLayoutSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity tilemapRow)
    {
        _sessionEntity = tilemapRow.World.RequireSingleEntityWith<Session>("Snake session");
        var renderer = _host.RendererRequired;

        ref var tilemap = ref tilemapRow.Get<Tilemap>();
        tilemap.AtlasAlbedoTextureId = renderer.WhiteTextureId;
        tilemap.Layer = (int)SpriteLayer.Background;
        tilemap.SortKey = 0f;
        tilemap.NonEmptyTileMinIndex = 1;
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity tilemapRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = tilemapRow.World;
        var fb = ModLayoutViewport.VirtualSizeForSimulation(_host);
        if (fb.X <= 0 || fb.Y <= 0) return;
        ref var session = ref world.Get<Session>(_sessionEntity);
        session.UpdateLayout(fb.X, fb.Y);
        ref var tilemap = ref tilemapRow.Get<Tilemap>();
        tilemap.TileWidth = session.Cell;
        tilemap.TileHeight = session.Cell;

        ref var tf = ref tilemapRow.Get<Transform>();
        tf.LocalPosition = WorldViewportSpace.ViewportPixelToWorldCenter(
            new Vector2D<float>(session.OriginX, session.OriginY),
            fb);
        tf.LocalRotationRadians = 0f;
        tf.LocalScale = new Vector2D<float>(1f, 1f);
    }
}
