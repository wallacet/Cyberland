namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// SoA column for one component type inside an <see cref="ArchetypeChunk"/>.
/// </summary>
internal abstract class ColumnBase
{
    public abstract void SwapRemove(int row, int lastRow);

    /// <summary>
    /// Copy value from this column at <paramref name="srcRow"/> into <paramref name="dest"/> at <paramref name="destRow"/>.
    /// </summary>
    public abstract void CopyRowTo(ColumnBase dest, int srcRow, int destRow);

    public abstract void WriteDefault(int row);
}

internal sealed class Column<T> : ColumnBase where T : struct
{
    private T[] _data;

    public Column(int capacity) => _data = new T[capacity];

    public Span<T> AsSpan(int count) => _data.AsSpan(0, count);

    public ref T At(int row) => ref _data[row];

    public override void SwapRemove(int row, int lastRow)
    {
        if (row != lastRow)
            _data[row] = _data[lastRow];
    }

    public override void CopyRowTo(ColumnBase dest, int srcRow, int destRow)
    {
        var d = (Column<T>)dest;
        d._data[destRow] = _data[srcRow];
    }

    public override void WriteDefault(int row) => _data[row] = default;
}
