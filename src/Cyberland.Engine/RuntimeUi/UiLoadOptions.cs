namespace Cyberland.Engine.RuntimeUi;

/// <summary>Options for <see cref="IUiRuntime.LoadDocumentAsync"/>.</summary>
public sealed class UiLoadOptions
{
    /// <summary>When true, unknown <c>type</c> strings are ignored instead of failing the build.</summary>
    public bool AllowUnknownElementTypes { get; set; }
}
