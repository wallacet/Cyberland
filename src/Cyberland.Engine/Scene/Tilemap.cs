using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS component describing how to draw one logical tile grid: cell size, texture, and sort layer. Cell indices live in <see cref="ITilemapDataStore"/>.
/// </summary>
/// <remarks>
/// Placement uses <see cref="Transform"/> (pivot at cell (0,0)’s top-left in viewport-style local offsets; see <see cref="Systems.TilemapRenderSystem"/>).
/// </remarks>
[RequiresComponent<Transform>]
public struct Tilemap : IComponent
{
    /// <summary>Width of one cell in world pixels.</summary>
    public float TileWidth;
    /// <summary>Height of one cell in world pixels.</summary>
    public float TileHeight;
    /// <summary>Shared albedo atlas for all non-empty tiles in this map.</summary>
    public TextureId AtlasAlbedoTextureId;
    /// <summary>Sprite layer for the whole grid pass.</summary>
    public int Layer;
    /// <summary>Sort key within the layer.</summary>
    public float SortKey;
    /// <summary>Minimum tile **value** treated as solid (values below draw empty).</summary>
    public int NonEmptyTileMinIndex;

    /// <summary>Atlas column count for tile-index to UV lookup (values &lt;= 0 mean "single full texture").</summary>
    public int AtlasColumns;

    /// <summary>Atlas row count for tile-index to UV lookup (values &lt;= 0 mean "single full texture").</summary>
    public int AtlasRows;
}
