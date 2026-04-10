using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene2D;

public sealed class TilemapDataStore : ITilemapDataStore
{
    private sealed class Entry
    {
        public int[] Tiles = Array.Empty<int>();
        public int Columns;
        public int Rows;
    }

    private readonly Dictionary<EntityId, Entry> _map = new();

    public void Register(EntityId owner, ReadOnlySpan<int> tileIndices, int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
            throw new ArgumentOutOfRangeException();
        if (tileIndices.Length != columns * rows)
            throw new ArgumentException("Tile buffer size must equal columns * rows.");

        var e = new Entry { Columns = columns, Rows = rows, Tiles = tileIndices.ToArray() };
        _map[owner] = e;
    }

    public void Unregister(EntityId owner) =>
        _map.Remove(owner);

    public bool TryGet(EntityId owner, out ReadOnlyMemory<int> tiles, out int columns, out int rows)
    {
        if (_map.TryGetValue(owner, out var e))
        {
            tiles = e.Tiles;
            columns = e.Columns;
            rows = e.Rows;
            return true;
        }

        tiles = default;
        columns = 0;
        rows = 0;
        return false;
    }
}
