namespace Cyberland.Engine.RuntimeUi;

/// <summary>Outcome of attaching a UI document to an ECS entity.</summary>
public sealed class UiAttachResult
{
    /// <summary>Whether load and registry registration succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Nested build result.</summary>
    public UiBuildResult? Build { get; init; }

    /// <summary>Human-readable failure reason when <see cref="Succeeded"/> is false.</summary>
    public string? ErrorMessage { get; init; }
}
