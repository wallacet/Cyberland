using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene2D;

/// <summary>Heap tile index grids keyed by entity; keeps large maps out of ECS chunks.</summary>
public interface ITilemapDataStore
{
    void Register(EntityId owner, ReadOnlySpan<int> tileIndices, int columns, int rows);
    void Unregister(EntityId owner);
    bool TryGet(EntityId owner, out ReadOnlyMemory<int> tiles, out int columns, out int rows);
}
