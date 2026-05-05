namespace Cyberland.Engine.Hosting;

/// <summary>
/// Frame queue for UI-driven gameplay intents drained after <see cref="Scene.Systems.UiDocumentFrameSystem"/> each render tick.
/// </summary>
public interface IUiCommandQueue
{
    /// <summary>Enqueues a command object (typically a small struct boxed once).</summary>
    void Enqueue(object command);

    /// <summary>Non-destructive peek at the next command without removing it.</summary>
    bool TryPeek(out object? command);

    /// <summary>Removes and returns the next command when the queue is non-empty.</summary>
    bool TryDequeue(out object? command);

    /// <summary>Commands waiting for <see cref="TryDequeue"/>.</summary>
    int Count { get; }
}
