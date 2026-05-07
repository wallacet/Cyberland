namespace Cyberland.Engine.Hosting;

/// <summary>
/// Frame queue for UI-driven gameplay intents drained after <see cref="Scene.Systems.UiDocumentFrameSystem"/> each render tick.
/// </summary>
/// <remarks>
/// Engine-managed usage is single-threaded: enqueue from serial UI systems and dequeue from
/// <see cref="Scene.Systems.UiCommandDrainSystem"/> on the same late-update phase. This interface is not a concurrent
/// queue contract and should not be called from worker-thread ECS systems.
/// </remarks>
public interface IUiCommandQueue
{
    /// <summary>Enqueues a typed command.</summary>
    void Enqueue(IUiCommand command);

    /// <summary>Non-destructive peek at the next command without removing it.</summary>
    bool TryPeek(out IUiCommand? command);

    /// <summary>Removes and returns the next command when the queue is non-empty.</summary>
    bool TryDequeue(out IUiCommand? command);

    /// <summary>Commands waiting for <see cref="TryDequeue"/>.</summary>
    int Count { get; }

    /// <summary>
    /// Drops oldest commands until at most <paramref name="maxCount"/> remain; returns the number removed.
    /// </summary>
    int TrimToMaxCount(int maxCount);
}
