namespace Cyberland.Engine.Scene;

/// <summary>
/// ECS component describing how to draw one logical tile grid: cell size, origin, texture, and sort layer. Cell indices live in <see cref="ITilemapDataStore"/>.
/// </summary>
public struct Tilemap
{
    /// <summary>Width of one cell in world pixels.</summary>
    public float TileWidth;
    /// <summary>Height of one cell in world pixels.</summary>
    public float TileHeight;
    /// <summary>World X of cell (0,0)’s corner (see tilemap layout conventions in <see cref="Systems.TilemapRenderSystem"/>).</summary>
    public float OriginX;
    /// <summary>World Y of cell (0,0)’s corner.</summary>
    public float OriginY;
    /// <summary>Shared albedo atlas for all non-empty tiles in this map.</summary>
    public int AtlasAlbedoTextureId;
    /// <summary>Sprite layer for the whole grid pass.</summary>
    public int Layer;
    /// <summary>Sort key within the layer.</summary>
    public float SortKey;
    /// <summary>Minimum tile **value** treated as solid (values below draw empty).</summary>
    public int NonEmptyTileMinIndex;
}
