namespace Cyberland.Engine.Rendering.Text;

/// <summary>One substring with its own style; may be literal or a localization key.</summary>
public readonly struct TextRun
{
    /// <summary>Literal text or a key into <see cref="Localization.LocalizationManager"/>.</summary>
    /// <param name="content">String data.</param>
    /// <param name="style">Visual style for this run.</param>
    /// <param name="isLocalizationKey">When true, <paramref name="content"/> is passed to <c>LocalizationManager.Get</c>.</param>
    public TextRun(string content, TextStyle style, bool isLocalizationKey = false)
    {
        Content = content;
        Style = style;
        IsLocalizationKey = isLocalizationKey;
    }

    /// <summary>Literal characters or a locale key.</summary>
    public string Content { get; }

    /// <summary>Draw style for this run.</summary>
    public TextStyle Style { get; }

    /// <summary>If true, resolve via localization before layout.</summary>
    public bool IsLocalizationKey { get; }
}
