namespace Cyberland.Engine.Hosting;

/// <summary>Thread-unsafe FIFO used from serial UI systems on the render thread.</summary>
public sealed class UiCommandQueue : IUiCommandQueue
{
    private readonly Queue<IUiCommand> _queue = new();

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public void Enqueue(IUiCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queue.Enqueue(command);
    }

    /// <inheritdoc />
    public bool TryPeek(out IUiCommand? command)
    {
        if (_queue.Count == 0)
        {
            command = null;
            return false;
        }

        command = _queue.Peek();
        return true;
    }

    /// <inheritdoc />
    public bool TryDequeue(out IUiCommand? command) => _queue.TryDequeue(out command);

    /// <inheritdoc />
    public int TrimToMaxCount(int maxCount)
    {
        if (maxCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "maxCount must be non-negative.");

        var removed = 0;
        while (_queue.Count > maxCount && _queue.TryDequeue(out _))
            removed++;
        return removed;
    }
}
