using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Snapshot of the last successful glyph prepare for a <see cref="BitmapText"/> row (diagnostics / future diffing).
/// </summary>
/// <remarks>
/// <para>
/// The name “fingerprint” is historical. The engine no longer uses these fields to skip layout in the runtime builder;
/// every visible row rebuilds from the resolved string each time <see cref="Systems.TextRenderSystem"/> runs.
/// </para>
/// <para>
/// Fields are still written each prepare so tooling can compare viewport/baseline/hash without introspecting strings.
/// <see cref="Systems.TextRuntimeBuilder"/> compares these against the current resolved row (excluding baseline-only moves)
/// to decide when to discard CPU glyph slots before rebuilding.
/// </para>
/// Adding this component via <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> also ensures <see cref="Transform"/> exists.
/// </remarks>
[RequiresComponent<Transform>]
public struct TextBuildFingerprint : IComponent
{
    /// <summary>64-bit ordinal hash of the resolved display string from the last successful glyph build.</summary>
    public ulong ResolvedContentHash64;

    /// <summary>UTF-16 code unit count of the resolved string from the last successful glyph build.</summary>
    /// <remarks>Stored alongside <see cref="ResolvedContentHash64"/> so hash collisions cannot hide length changes in diagnostics.</remarks>
    public int ResolvedCharCount;

    /// <summary>Hash of <see cref="BitmapText.Style"/> from the last successful glyph build.</summary>
    public int StyleHash;

    /// <summary><see cref="BitmapText.CoordinateSpace"/> at the last successful glyph build.</summary>
    public CoordinateSpace CoordinateSpace;

    /// <summary><see cref="BitmapText.SortKey"/> at the last successful glyph build.</summary>
    public float SortKey;

    /// <summary>World-space baseline X used for the last successful glyph build.</summary>
    public float BaselineWorldX;

    /// <summary>World-space baseline Y used for the last successful glyph build.</summary>
    public float BaselineWorldY;

    /// <summary>Framebuffer width in pixels when <see cref="BitmapText.CoordinateSpace"/> was viewport space; otherwise 0.</summary>
    public int FramebufferW;

    /// <summary>Framebuffer height in pixels when <see cref="BitmapText.CoordinateSpace"/> was viewport space; otherwise 0.</summary>
    public int FramebufferH;
}
