using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// One-line anchor configurations for common HUD/layout cases (Unity-style anchors, +Y down).
/// </summary>
public static class UiLayoutPresets
{
    /// <summary>Fills the parent content slot (stretch on both axes).</summary>
    public static void StretchAll(UiElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(0f, 0f);
        element.AnchorMax = new Vector2D<float>(1f, 1f);
        element.Pivot = new Vector2D<float>(0f, 0f);
        element.AnchoredPosition = default;
        element.SizeDelta = default;
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }

    /// <summary>Full width along the top edge; height comes from <paramref name="height"/>.</summary>
    public static void TopStretch(UiElement element, float height)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(0f, 0f);
        element.AnchorMax = new Vector2D<float>(1f, 0f);
        element.Pivot = new Vector2D<float>(0f, 0f);
        element.AnchoredPosition = default;
        element.SizeDelta = new Vector2D<float>(0f, height);
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }

    /// <summary>Fixed size box pinned to the top-left of the parent content area.</summary>
    public static void TopLeftFixed(UiElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(0f, 0f);
        element.AnchorMax = new Vector2D<float>(0f, 0f);
        element.Pivot = new Vector2D<float>(0f, 0f);
        element.AnchoredPosition = default;
        element.SizeDelta = new Vector2D<float>(width, height);
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }

    /// <summary>Fixed size centered in the parent content area.</summary>
    public static void CenterFixed(UiElement element, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(0.5f, 0.5f);
        element.AnchorMax = new Vector2D<float>(0.5f, 0.5f);
        element.Pivot = new Vector2D<float>(0.5f, 0.5f);
        element.AnchoredPosition = default;
        element.SizeDelta = new Vector2D<float>(width, height);
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }

    /// <summary>Fixed size box pinned to the top-right with pixel <paramref name="margin"/> inset from edges.</summary>
    public static void TopRightFixed(UiElement element, float width, float height, float margin)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(1f, 0f);
        element.AnchorMax = new Vector2D<float>(1f, 0f);
        element.Pivot = new Vector2D<float>(1f, 0f);
        element.AnchoredPosition = new Vector2D<float>(-margin, margin);
        element.SizeDelta = new Vector2D<float>(width, height);
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }

    /// <summary>Fixed size box pinned to the bottom-right with pixel <paramref name="margin"/> inset from edges.</summary>
    public static void BottomRightFixed(UiElement element, float width, float height, float margin)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.AnchorMin = new Vector2D<float>(1f, 1f);
        element.AnchorMax = new Vector2D<float>(1f, 1f);
        element.Pivot = new Vector2D<float>(1f, 1f);
        element.AnchoredPosition = new Vector2D<float>(-margin, -margin);
        element.SizeDelta = new Vector2D<float>(width, height);
        element.StretchLeft = element.StretchRight = element.StretchTop = element.StretchBottom = 0f;
    }
}
