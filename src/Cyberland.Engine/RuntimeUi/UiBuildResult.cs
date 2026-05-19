using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Outcome of building a UI document from JSON.</summary>
public sealed class UiBuildResult
{
    /// <summary>Whether the build completed without error.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Built document when <see cref="Succeeded"/> is true.</summary>
    public UiDocument? Document { get; init; }

    /// <summary>Session used during the build (ids, radio groups).</summary>
    public UiBuildSession? Session { get; init; }

    /// <summary>Human-readable failure reason when <see cref="Succeeded"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Convenience map when <see cref="Session"/> is non-null.</summary>
    public IReadOnlyDictionary<string, UiElement> ElementsById =>
        Session?.ElementsById ?? EmptyElements;

    private static readonly IReadOnlyDictionary<string, UiElement> EmptyElements =
        new Dictionary<string, UiElement>();
}
