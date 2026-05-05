namespace Cyberland.Engine.Hosting;

/// <summary>Thread-unsafe FIFO used from serial UI systems on the render thread.</summary>
public sealed class UiCommandQueue : IUiCommandQueue
{
    private readonly Queue<object> _queue = new();

    /// <inheritdoc />
    public int Count => _queue.Count;

    /// <inheritdoc />
    public void Enqueue(object command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queue.Enqueue(command);
    }

    /// <inheritdoc />
    public bool TryPeek(out object? command)
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
    public bool TryDequeue(out object? command) => _queue.TryDequeue(out command);
}
