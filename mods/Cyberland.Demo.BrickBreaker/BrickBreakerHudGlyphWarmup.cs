using System.Text;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Seeds <see cref="TextGlyphCache"/> with glyphs used by BrickBreaker HUD so gameplay avoids synchronous MSDF generation and
/// first-atlas-page GPU uploads during ball/brick frames (common source of multi-ms spikes in profiler dumps).
/// </summary>
internal static class BrickBreakerHudGlyphWarmup
{
    private static readonly string[] s_localizedHudKeys =
    {
        "demo.brick.title",
        "demo.brick.hint_title",
        "demo.brick.you_win",
        "demo.brick.game_over",
        "demo.brick.hint_win",
        "demo.brick.hint_gameover",
        "demo.brick.playing_score"
    };

    internal static void Warm(ModLoadContext context)
    {
        var host = context.Host;
        var renderer = host.Renderer;
        var fonts = host.Fonts;
        var cache = host.TextGlyphCache;
        var strings = host.LocalizedContent?.Strings;
        if (renderer is null || strings is null)
            return;

        Span<char> utf16 = stackalloc char[2];

        foreach (var key in s_localizedHudKeys)
        {
            var style = StyleForLocalizationKey(key);
            WarmString(cache, renderer, fonts, style, strings.Get(key), utf16);
        }

        // FPS row matches SceneSetup HudTextRow default: UiSans 15 (builtin MSDF atlas), white.
        var fpsStyle = new TextStyle(BuiltinFonts.UiSans, 15f, new Vector4D<float>(1f, 1f, 1f, 1f));
        WarmString(cache, renderer, fonts, fpsStyle, Constants.FpsHudAwaitingLabel, utf16);
        for (var d = '0'; d <= '9'; d++)
            WarmString(cache, renderer, fonts, fpsStyle, $"FPS {d}", utf16);
        WarmString(cache, renderer, fonts, fpsStyle, "FPS 99", utf16);
        WarmString(cache, renderer, fonts, fpsStyle, "FPS 999", utf16);

        for (var d = '0'; d <= '9'; d++)
            TryGlyph(cache, renderer, fonts, HudTextStyles.Score, d, utf16);
    }

    private static TextStyle StyleForLocalizationKey(string key) => key switch
    {
        "demo.brick.title" => HudTextStyles.Title,
        "demo.brick.hint_title" => HudTextStyles.Hint,
        "demo.brick.you_win" or "demo.brick.game_over" => HudTextStyles.GameOver,
        "demo.brick.hint_win" or "demo.brick.hint_gameover" => HudTextStyles.Hint,
        "demo.brick.playing_score" => HudTextStyles.Hud,
        _ => HudTextStyles.Hud
    };

    private static void WarmString(
        TextGlyphCache cache,
        IRenderer renderer,
        FontLibrary fonts,
        TextStyle style,
        string text,
        Span<char> utf16Scratch)
    {
        if (string.IsNullOrEmpty(text))
            return;

        foreach (var rune in text.EnumerateRunes())
            TryGlyph(cache, renderer, fonts, style, rune, utf16Scratch);
    }

    private static void TryGlyph(
        TextGlyphCache cache,
        IRenderer renderer,
        FontLibrary fonts,
        TextStyle style,
        Rune rune,
        Span<char> utf16Scratch)
    {
        var n = rune.EncodeToUtf16(utf16Scratch);
        _ = cache.TryGetGlyph(renderer, fonts, in style, rune.Value, utf16Scratch[..n], out _);
    }

    private static void TryGlyph(
        TextGlyphCache cache,
        IRenderer renderer,
        FontLibrary fonts,
        TextStyle style,
        char ch,
        Span<char> utf16Scratch)
    {
        utf16Scratch[0] = ch;
        _ = cache.TryGetGlyph(renderer, fonts, in style, ch, utf16Scratch[..1], out _);
    }
}
