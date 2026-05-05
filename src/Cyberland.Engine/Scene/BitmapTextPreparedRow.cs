using Cyberland.Engine.Scene.Systems;

namespace Cyberland.Engine.Scene;

/// <summary>
/// CPU-side hooks for <see cref="BitmapText"/> rows that cache shaped glyphs in <see cref="TextSpriteCache"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Systems.TextRenderSystem"/> resolves text and fills <see cref="TextSpriteCache"/> immediately before
/// <see cref="Rendering.IRenderer.SubmitSprites"/> each frame. The runtime builder drops prepared CPU slots automatically
/// when resolved copy / style / layer inputs change vs the last successful prepare; normal <see cref="BitmapText.Content"/>
/// edits do not require this API.
/// Use <see cref="DiscardPrepared"/> only for exceptional cases (ordering hazards outside text-render, hot reload, tests).
/// </para>
/// <para>
/// Clearing CPU caches does not address GPU persistence by itself (HDR bloom smear, composite/load barriers, driver
/// quirks). Those are renderer/post paths; treat them separately when diagnosing “ghost” HUD pixels.
/// </para>
/// </remarks>
public static class BitmapTextPreparedRow
{
    /// <summary>
    /// Zeros glyph submission state and resets the layout fingerprint so the next successful prepare starts clean.
    /// </summary>
    public static void DiscardPrepared(ref TextSpriteCache cache, ref TextBuildFingerprint fingerprint) =>
        TextRuntimeBuilder.DiscardPreparedRow(ref cache, ref fingerprint);
}
