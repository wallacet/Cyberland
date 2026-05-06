namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Shared text-MSDF generation/reconstruction defaults.
/// </summary>
/// <remarks>
/// The baseline matrices are used by tests to ensure we keep validating readability-critical
/// font sizes and viewport-to-swapchain scales used by HUD/UI demos.
/// </remarks>
internal static class TextMsdfDefaults
{
    public const int AtlasSupersample = 2;
    public const int AtlasSupersampleSmallGlyph = 3;
    public const float AtlasSupersampleSmallGlyphMaxDrawPixels = 48f;
    public const int BorderPixels = 12;
    public const float PixelRange = 8f;
    public const float EdgeSharpness = 1.3f;

    public static ReadOnlySpan<float> BaselineSizesPx => [12f, 14f, 18f, 24f];
    public static ReadOnlySpan<float> BaselinePhysicalScales => [1.0f, 1.25f, 1.5f, 2.0f];
}
