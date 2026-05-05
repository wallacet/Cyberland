namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Primary-axis alignment for horizontal/vertical stack layout peers (<c>UiHorizontalStack</c>, <c>UiVerticalStack</c>).
/// </summary>
public enum UiAlignment
{
    /// <summary>Pack toward start of main axis.</summary>
    Start,

    /// <summary>Center along main axis.</summary>
    Center,

    /// <summary>Pack toward end of main axis.</summary>
    End,

    /// <summary>Expand to fill extra space on main axis when applicable.</summary>
    Stretch
}

/// <summary>
/// Cross-axis alignment for horizontal/vertical stack layout peers (<c>UiHorizontalStack</c>, <c>UiVerticalStack</c>).
/// </summary>
public enum UiCrossAlignment
{
    /// <summary>Pack toward start of cross axis.</summary>
    Start,

    /// <summary>Center on cross axis.</summary>
    Center,

    /// <summary>Pack toward end of cross axis.</summary>
    End,

    /// <summary>Expand to fill cross-axis extent.</summary>
    Stretch
}

/// <summary>
/// How this element clips descendants during draw and hit-test.
/// </summary>
public enum UiClipMode
{
    /// <summary>No clipping beyond inherited behavior.</summary>
    None,

    /// <summary>Clip to this element's content rectangle intersected with parent clip.</summary>
    IntersectParent
}
