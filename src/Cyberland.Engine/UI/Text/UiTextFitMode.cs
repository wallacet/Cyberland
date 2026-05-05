namespace Cyberland.Engine.UI.Text;

/// <summary>
/// Which axes must satisfy the box when <see cref="UiTextFitMode.ShrinkToFit"/> searches font sizes.
/// </summary>
public enum UiTextFitTarget
{
    /// <summary>Require wrapped line width ≤ content width (height may exceed).</summary>
    WidthOnly,

    /// <summary>Require layout width and height ≤ content rect.</summary>
    Box
}

/// <summary>
/// How <see cref="UiTextBlock"/> scales text to fit its box.
/// </summary>
public enum UiTextFitMode
{
    /// <summary>Use nominal text style pixel size (no fitting).</summary>
    None,

    /// <summary>
    /// Binary-search quantized sizes between <see cref="UiTextBlock.MinFitSizePixels"/> and the nominal reference size.
    /// </summary>
    ShrinkToFit
}
