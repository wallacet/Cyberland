using System;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Engine.UI.Text;

internal readonly record struct StyledFragment(string Text, TextStyle Style);

internal enum UiTextTokenKind
{
    Word,
    HardLineBreak
}

internal readonly struct UiTextToken(UiTextTokenKind kind, string text, TextStyle style)
{
    public UiTextTokenKind Kind { get; } = kind;
    public string Text { get; } = text;
    public TextStyle Style { get; } = style;
}

internal sealed class UiTextLayoutLine
{
    public readonly List<(string Text, TextStyle Style, float PenStart)> Segments = new();

    /// <summary>Top of the line box (+Y down, relative to content origin).</summary>
    public float LineTop { get; set; }

    public float MaxLineHeight(FontLibrary fonts)
    {
        var h = 0f;
        foreach (var seg in Segments)
            h = MathF.Max(h, UiTextMeasurer.MeasureLineHeight(fonts, seg.Style));

        return h;
    }
}

/// <summary>Lays out wrapped lines (+Y down) for <see cref="UiTextBlock"/>.</summary>
internal sealed class UiTextLayoutEngine
{
    private UiTextLayoutEngine(float totalWidth, float totalHeight, List<UiTextLayoutLine> lines, float maxLineAdvance)
    {
        TotalWidth = totalWidth;
        TotalHeight = totalHeight;
        Lines = lines;
        MaxLineAdvance = maxLineAdvance;
    }

    public float TotalWidth { get; }

    /// <summary>Unclamped widest line advance (for shrink-to-fit width tests).</summary>
    public float MaxLineAdvance { get; }

    public float TotalHeight { get; }
    public IReadOnlyList<UiTextLayoutLine> Lines { get; }

    public static UiTextLayoutEngine Build(
        FontLibrary fonts,
        LocalizationManager? localization,
        string? singleText,
        TextStyle defaultStyle,
        IReadOnlyList<TextRun>? runs,
        float maxContentWidth,
        float paragraphSpacing,
        float lineSpacingExtra)
    {
        var paragraphs = BuildParagraphs(singleText, defaultStyle, runs, localization);
        var lines = new List<UiTextLayoutLine>();
        var maxLineW = 0f;
        float contentBottom = 0f;

        for (var pi = 0; pi < paragraphs.Count; pi++)
        {
            var para = paragraphs[pi];
            if (para.Count == 0)
                continue;

            var tokens = FlattenParagraphTokens(para);
            if (tokens.Count == 0)
                continue;

            WrapParagraph(fonts, tokens, maxContentWidth, lines, ref contentBottom, lineSpacingExtra, ref maxLineW);

            if (pi < paragraphs.Count - 1)
                contentBottom += paragraphSpacing;
        }

        if (lines.Count == 0)
            return new UiTextLayoutEngine(0f, 0f, lines, 0f);

        return new UiTextLayoutEngine(MathF.Min(maxContentWidth, maxLineW), contentBottom, lines, maxLineW);
    }

    private static List<List<StyledFragment>> BuildParagraphs(
        string? singleText,
        TextStyle defaultStyle,
        IReadOnlyList<TextRun>? runs,
        LocalizationManager? localization)
    {
        var paragraphs = new List<List<StyledFragment>>();
        if (runs is { Count: > 0 })
        {
            List<StyledFragment>? cur = new();
            foreach (var run in runs)
            {
                var t = run.IsLocalizationKey && localization is not null
                    ? localization.Get(run.Content)
                    : run.Content;

                if (string.IsNullOrEmpty(t))
                    continue;

                var parts = t.Split("\n\n", StringSplitOptions.None);
                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                    {
                        paragraphs.Add(cur!);
                        cur = new List<StyledFragment>();
                    }

                    var piece = parts[i];
                    if (piece.Length > 0)
                        cur!.Add(new StyledFragment(piece, run.Style));
                }
            }

            if (cur is { Count: > 0 })
                paragraphs.Add(cur);

            return paragraphs;
        }

        var text = singleText ?? string.Empty;
        if (text.Length == 0)
            return paragraphs;

        foreach (var block in text.Split("\n\n", StringSplitOptions.None))
        {
            var p = new List<StyledFragment>();
            if (block.Length > 0)
                p.Add(new StyledFragment(block, defaultStyle));

            paragraphs.Add(p);
        }

        return paragraphs;
    }

    private static List<UiTextToken> FlattenParagraphTokens(List<StyledFragment> paragraph)
    {
        var tokens = new List<UiTextToken>();
        foreach (var frag in paragraph)
        {
            var parts = frag.Text.Split('\n');
            for (var hi = 0; hi < parts.Length; hi++)
            {
                if (hi > 0)
                    tokens.Add(new UiTextToken(UiTextTokenKind.HardLineBreak, string.Empty, frag.Style));

                var segment = parts[hi];
                foreach (var word in segment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    tokens.Add(new UiTextToken(UiTextTokenKind.Word, word, frag.Style));
            }
        }

        return tokens;
    }

    private static void WrapParagraph(
        FontLibrary fonts,
        List<UiTextToken> tokens,
        float maxContentWidth,
        List<UiTextLayoutLine> lines,
        ref float contentBottom,
        float lineSpacingExtra,
        ref float maxLineW)
    {
        var pending = new List<(string Text, TextStyle Style, float PenStart)>();
        float pen = 0f;

        foreach (var tok in tokens)
        {
            if (tok.Kind == UiTextTokenKind.HardLineBreak)
            {
                FlushLine(fonts, pending, lines, ref contentBottom, lineSpacingExtra, ref maxLineW, ref pen);
                continue;
            }

            var word = tok.Text;
            var st = tok.Style;
            var wordW = UiTextMeasurer.MeasureAdvanceWidth(fonts, in st, word.AsSpan());
            var spaceW = pen > 0f
                ? UiTextMeasurer.MeasureAdvanceWidth(fonts, in st, " ".AsSpan())
                : 0f;

            var extra = pen > 0f ? spaceW : 0f;
            if (pending.Count > 0 && pen + extra + wordW > maxContentWidth)
                FlushLine(fonts, pending, lines, ref contentBottom, lineSpacingExtra, ref maxLineW, ref pen);

            if (pending.Count > 0 && extra > 0f)
                pen += extra;

            if (pending.Count == 0 && wordW > maxContentWidth && maxContentWidth > 0f)
            {
                pending.Add((word, st, 0f));
                pen = wordW;
                FlushLine(fonts, pending, lines, ref contentBottom, lineSpacingExtra, ref maxLineW, ref pen);
                continue;
            }

            pending.Add((word, st, pen));
            pen += wordW;
        }

        FlushLine(fonts, pending, lines, ref contentBottom, lineSpacingExtra, ref maxLineW, ref pen);
    }

    private static void FlushLine(
        FontLibrary fonts,
        List<(string Text, TextStyle Style, float PenStart)> pending,
        List<UiTextLayoutLine> lines,
        ref float contentBottom,
        float lineSpacingExtra,
        ref float maxLineW,
        ref float pen)
    {
        if (pending.Count == 0)
            return;

        var lh = 0f;
        foreach (var s in pending)
            lh = MathF.Max(lh, UiTextMeasurer.MeasureLineHeight(fonts, s.Style));

        maxLineW = MathF.Max(maxLineW, pen);

        var line = new UiTextLayoutLine { LineTop = contentBottom };
        line.Segments.AddRange(pending);
        lines.Add(line);

        contentBottom += lh + lineSpacingExtra;
        pending.Clear();
        pen = 0f;
    }

    /// <summary>Hash fingerprint for <see cref="UiTextLayoutCache"/> invalidation.</summary>
    public static int ComputeFingerprint(
        string? text,
        TextStyle defaultStyle,
        IReadOnlyList<TextRun>? runs,
        float maxContentWidth,
        float paragraphSpacing,
        float lineSpacingExtra)
    {
        var h = new HashCode();
        h.Add(text);
        AddTextStyle(ref h, in defaultStyle);
        h.Add(paragraphSpacing);
        h.Add(lineSpacingExtra);
        h.Add(Quantize(maxContentWidth));
        if (runs is not null)
        {
            foreach (var r in runs)
            {
                h.Add(r.Content);
                h.Add(r.IsLocalizationKey);
                var runStyle = r.Style;
                AddTextStyle(ref h, in runStyle);
            }
        }

        return h.ToHashCode();
    }

    private static void AddTextStyle(ref HashCode h, in TextStyle s)
    {
        h.Add(s.FontFamilyId);
        h.Add(s.SizePixels);
        h.Add(s.Color.X);
        h.Add(s.Color.Y);
        h.Add(s.Color.Z);
        h.Add(s.Color.W);
        h.Add(s.Bold);
        h.Add(s.Italic);
        h.Add(s.Underline);
        h.Add(s.Strikethrough);
    }

    private static int Quantize(float w) => (int)MathF.Round(w * 4f);
}

/// <summary>Holds the last computed <see cref="UiTextLayoutEngine"/> between measure passes.</summary>
internal sealed class UiTextLayoutCache
{
    public UiTextLayoutEngine? Layout { get; private set; }
    public int Fingerprint { get; private set; }

    public bool TryGet(int fingerprint, out UiTextLayoutEngine? layout)
    {
        if (Layout is not null && fingerprint == Fingerprint)
        {
            layout = Layout;
            return true;
        }

        layout = null;
        return false;
    }

    public void Store(int fingerprint, UiTextLayoutEngine layout)
    {
        Fingerprint = fingerprint;
        Layout = layout;
    }

    public void Clear()
    {
        Layout = null;
        Fingerprint = 0;
    }
}
