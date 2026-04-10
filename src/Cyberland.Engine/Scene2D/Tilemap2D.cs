namespace Cyberland.Engine.Scene2D;

/// <summary>Metadata for a tile grid registered in <see cref="ITilemapDataStore"/>.</summary>
public struct Tilemap
{
    public float TileWidth;
    public float TileHeight;
    public float OriginX;
    public float OriginY;
    public int AtlasAlbedoTextureId;
    public int Layer;
    public float SortKey;
    /// <summary>Non-zero tile index draws a sprite; 0 or negative skips (empty).</summary>
    public int NonEmptyTileMinIndex;
}
