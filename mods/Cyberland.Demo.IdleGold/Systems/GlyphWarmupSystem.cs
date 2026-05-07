using Cyberland.Demo.IdleGold.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using System.Text;

namespace Cyberland.Demo.IdleGold.Systems;

/// <summary>
/// Warms a small set of IdleGold UI glyphs over several frames so first tab switches avoid cold MSDF spikes
/// without blocking startup on the white window.
/// </summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class GlyphWarmupSystem : ISingletonSystem, ISingletonLateUpdate
{
    private readonly Queue<(TextStyle Style, Rune Rune)> _pending = new();
    private readonly GameHostServices _host;
    private readonly char[] _utf16Buffer = new char[2];
    private bool _loggedComplete;
    private bool _startedAfterFirstPresent;

    public GlyphWarmupSystem(LocalizationManager loc, GameHostServices host)
    {
        _host = host;
        var strings = BuildWarmupStrings(loc);
        EnqueueStyles(strings, new TextStyle(BuiltinFonts.UiSans, 13f, default));
        EnqueueStyles(strings, new TextStyle(BuiltinFonts.UiSans, 14f, default));
        EnqueueStyles(strings, new TextStyle(BuiltinFonts.UiSans, 15f, default));
        EnqueueStyles(strings, new TextStyle(BuiltinFonts.UiSans, 18f, default, Bold: true));
        EnqueueStyles(strings, new TextStyle(BuiltinFonts.UiSans, 22f, default));
    }

    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag>();

    public void OnSingletonStart(in SingletonEntity stateRow) => _ = stateRow.Entity;

    public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
    {
        _ = row;
        _ = deltaSeconds;
        if (_pending.Count == 0)
        {
            if (!_loggedComplete)
            {
                _loggedComplete = true;
                Console.WriteLine("IdleGold glyph warmup complete");
            }

            return;
        }

        if (!_startedAfterFirstPresent)
        {
            if (_host.LastPresentDeltaSeconds <= 0f)
                return;
            _startedAfterFirstPresent = true;
        }

        // Keep warmup cheap on slower machines: skip if the last frame was already over budget.
        if (_host.LastPresentDeltaSeconds > 0.02f)
            return;

        const int maxGlyphsPerFrame = 6;
        var renderer = _host.Renderer;
        for (var i = 0; i < maxGlyphsPerFrame && _pending.Count > 0; i++)
        {
            var next = _pending.Dequeue();
            var written = next.Rune.EncodeToUtf16(_utf16Buffer);
            _host.TextGlyphCache.TryGetGlyph(renderer, _host.Fonts, in next.Style, next.Rune.Value, _utf16Buffer.AsSpan(0, written), out _);
        }
    }

    private void EnqueueStyles(IReadOnlyCollection<string> strings, in TextStyle style)
    {
        var seen = new HashSet<int>();
        foreach (var text in strings)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                if (seen.Add(rune.Value))
                    _pending.Enqueue((style, rune));
            }
        }
    }

    private static IReadOnlyCollection<string> BuildWarmupStrings(LocalizationManager loc) =>
    [
        loc.Get("idlegold.ui.title"),
        loc.Get("idlegold.nav.gather"),
        loc.Get("idlegold.nav.character"),
        loc.Get("idlegold.nav.blacksmith"),
        loc.Get("idlegold.nav.log"),
        loc.Get("idlegold.ui.gather_intro"),
        loc.Get("idlegold.ui.character_intro"),
        loc.Get("idlegold.ui.blacksmith_intro"),
        loc.Get("idlegold.ui.level"),
        loc.Get("idlegold.ui.rate"),
        loc.Get("idlegold.ui.total_rate"),
        loc.Get("idlegold.ui.level_up"),
        loc.Get("idlegold.ui.train"),
        loc.Get("idlegold.ui.unlock"),
        loc.Get("idlegold.ui.upgrade"),
        loc.Get("idlegold.ui.max_tier"),
        loc.Get("idlegold.ui.gold_per_sec")
    ];
}
