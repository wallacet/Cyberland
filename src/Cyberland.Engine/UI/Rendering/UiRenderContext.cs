using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.UI.Rendering;

/// <summary>
/// Clip stack and lightweight debug recording for UI draw passes (viewport-local +Y down).
/// </summary>
public sealed class UiRenderContext
{
    private readonly Stack<UiRect> _clipStack = new();

    /// <summary>Creates a context rooted at <paramref name="rootClip"/>.</summary>
    public UiRenderContext(UiRect rootClip) => _clipStack.Push(rootClip);

    /// <summary>Rects recorded by elements intersected with the active clip (tests / debug overlays).</summary>
    public List<UiRect> DebugRects { get; } = new();

    /// <summary>Current clipping rectangle.</summary>
    public UiRect CurrentClip => _clipStack.Peek();

    /// <summary>Records <paramref name="bounds"/> clipped to <see cref="CurrentClip"/>.</summary>
    public void RecordDebugRect(in UiRect bounds) =>
        DebugRects.Add(bounds.Intersect(CurrentClip));

    /// <summary>Pushes a new clip rectangle.</summary>
    public void PushClip(in UiRect clip) => _clipStack.Push(clip);

    /// <summary>Pops the clip stack; throws when only the root clip remains.</summary>
    public void PopClip()
    {
        if (_clipStack.Count <= 1)
            throw new InvalidOperationException("Unbalanced UiRenderContext clip stack.");

        _clipStack.Pop();
    }
}
