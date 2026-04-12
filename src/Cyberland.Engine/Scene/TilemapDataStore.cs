using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>Default <see cref="ITilemapDataStore"/> used by the host when <see cref="Hosting.GameHostServices.Tilemaps"/> is assigned.</summary>
public sealed class TilemapDataStore : ITilemapDataStore
{
    private sealed class Entry
    {
        public int[] Tiles = Array.Empty<int>();
        public int Columns;
        public int Rows;
    }

    private readonly Dictionary<EntityId, Entry> _map = new();

    /// <inheritdoc />
    /// <remarks>Repeated <see cref="Register"/> for the same <paramref name="owner"/> and grid size reuses the backing <c>int[]</c> when possible.</remarks>
    public void Register(EntityId owner, ReadOnlySpan<int> tileIndices, int columns, int rows)
    {
        if (columns <= 0 || rows <= 0)
            throw new ArgumentOutOfRangeException();
        if (tileIndices.Length != columns * rows)
            throw new ArgumentException("Tile buffer size must equal columns * rows.");

        var len = columns * rows;
        if (!_map.TryGetValue(owner, out var e))
            e = new Entry();

        if (e.Tiles.Length != len)
            e.Tiles = new int[len];

        tileIndices.CopyTo(e.Tiles);
        e.Columns = columns;
        e.Rows = rows;
        _map[owner] = e;
    }

    /// <inheritdoc />
    public void Unregister(EntityId owner) =>
        _map.Remove(owner);

    /// <inheritdoc />
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
