using Silk.NET.Maths;

namespace Cyberland.Engine.UI.Core;

/// <summary>
/// Resolves Unity-style anchors within a parent slot (+Y down).
/// </summary>
public static class UiAnchorLayout
{
    private const float CollapsedEpsilon = 1e-4f;

    /// <summary>
    /// Computes border-box <see cref="UiRect"/> from anchor fields and optional stretch insets.
    /// </summary>
    /// <param name="slot">Region available after margin (anchor reference space).</param>
    /// <param name="anchorMin">Normalized anchor corner (0–1).</param>
    /// <param name="anchorMax">Normalized anchor corner (0–1).</param>
    /// <param name="pivot">Normalized pivot on the resolved rect (0–1).</param>
    /// <param name="anchoredPosition">Pixel offset applied at the anchor point (collapsed mode).</param>
    /// <param name="sizeDelta">Pixel width/height when collapsed; stretch mode ignores width/height from this.</param>
    /// <param name="stretchLeft">Inset from left edge when stretched.</param>
    /// <param name="stretchRight">Inset from right edge when stretched.</param>
    /// <param name="stretchTop">Inset from top edge when stretched.</param>
    /// <param name="stretchBottom">Inset from bottom edge when stretched.</param>
    public static UiRect ResolveBounds(
        in UiRect slot,
        Vector2D<float> anchorMin,
        Vector2D<float> anchorMax,
        Vector2D<float> pivot,
        Vector2D<float> anchoredPosition,
        Vector2D<float> sizeDelta,
        float stretchLeft,
        float stretchRight,
        float stretchTop,
        float stretchBottom)
    {
        var stretchX = anchorMax.X - anchorMin.X > CollapsedEpsilon;
        var stretchY = anchorMax.Y - anchorMin.Y > CollapsedEpsilon;

        float x;
        float y;
        float w;
        float h;

        if (stretchX)
        {
            x = slot.X + anchorMin.X * slot.Width + stretchLeft;
            w = (anchorMax.X - anchorMin.X) * slot.Width - stretchLeft - stretchRight;
            w = MathF.Max(0f, w);
        }
        else
        {
            w = MathF.Max(0f, sizeDelta.X);
            var anchorX = slot.X + anchorMin.X * slot.Width;
            x = anchorX + anchoredPosition.X - pivot.X * w;
        }

        if (stretchY)
        {
            y = slot.Y + anchorMin.Y * slot.Height + stretchTop;
            h = (anchorMax.Y - anchorMin.Y) * slot.Height - stretchTop - stretchBottom;
            h = MathF.Max(0f, h);
        }
        else
        {
            // Stretch width + collapsed Y with SizeDelta.Y≈0: height follows the parent slot (intrinsic measure path).
            // Fixed pixel height still uses SizeDelta.Y (> 0), e.g. UiLayoutPresets.TopStretch.
            float hResolved;
            if (stretchX && sizeDelta.Y <= CollapsedEpsilon && slot.Height > CollapsedEpsilon)
                hResolved = slot.Height;
            else
                hResolved = MathF.Max(0f, sizeDelta.Y);

            var anchorY = slot.Y + anchorMin.Y * slot.Height;
            y = anchorY + anchoredPosition.Y - pivot.Y * hResolved;
            h = hResolved;
        }

        return new UiRect(x, y, w, h);
    }
}
