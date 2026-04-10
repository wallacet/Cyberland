using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Optional side storage for big tile grids: maps <see cref="EntityId"/> → row-major <c>int[]</c> indices so <see cref="Tilemap"/> components stay small.
/// </summary>
public interface ITilemapDataStore
{
    /// <summary>Copies <paramref name="tileIndices"/> into a heap buffer owned by the store (replaces any prior registration for <paramref name="owner"/>).</summary>
    /// <param name="owner">Entity that also has a <see cref="Tilemap"/> component.</param>
    /// <param name="tileIndices">Row-major length <c>columns * rows</c>.</param>
    /// <param name="columns">Width of the grid in tiles.</param>
    /// <param name="rows">Height of the grid in tiles.</param>
    void Register(EntityId owner, ReadOnlySpan<int> tileIndices, int columns, int rows);

    /// <summary>Removes the grid for <paramref name="owner"/> if present.</summary>
    void Unregister(EntityId owner);

    /// <summary>Reads the stored grid without copying (valid until the next <see cref="Register"/> / <see cref="Unregister"/>).</summary>
    bool TryGet(EntityId owner, out ReadOnlyMemory<int> tiles, out int columns, out int rows);
}
