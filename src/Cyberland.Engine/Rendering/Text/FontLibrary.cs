using System.Globalization;
using Cyberland.Engine.Assets;
using SixLabors.Fonts;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Runtime registry of named font families loaded from TTF/OTF bytes (mods) or embedded built-ins.
/// </summary>
/// <remarks>
/// Typical mod flow: <c>context.MountDefaultContent()</c>, load bytes with <see cref="AssetManager.LoadBytesAsync"/>,
/// then <see cref="RegisterFamilyFromBytes"/> (or <see cref="RegisterFamilyFromVirtualPathsAsync"/>). Use the same
/// logical family id string in <see cref="TextStyle.FontFamilyId"/> when drawing.
/// </remarks>
public sealed class FontLibrary
{
    private readonly Dictionary<string, RegisteredFamily> _families = new(StringComparer.Ordinal);

    /// <summary>
    /// Reuses <see cref="Font"/> instances for layout/measure so word-wrap and HUD paths do not allocate a new font per
    /// span (must match <see cref="TextGlyphCache"/> face/size quantization so metrics align with rasterized glyphs).
    /// </summary>
    private readonly Dictionary<(string FamilyId, FontFaceKind Face, int SizeQuant), Font> _measureFontCache = new();

    /// <summary>
    /// SixLabors <see cref="Font"/> / measurers are not safe for concurrent use on the same instance; layout holds this
    /// while measuring and <see cref="TextGlyphCache"/> holds it while rasterizing so threads never interleave.
    /// </summary>
    private readonly object _measureFontLock = new();

    /// <summary>Sync root shared with UI measure helpers and <see cref="TextGlyphCache"/>.</summary>
    internal object FontRasterSync => _measureFontLock;

    /// <summary>Registers a family from in-memory font files (one stream per face).</summary>
    /// <param name="familyId">Stable id used in <see cref="TextStyle"/>.</param>
    /// <param name="regular">Required regular face bytes.</param>
    /// <param name="bold">Optional bold face.</param>
    /// <param name="italic">Optional italic face.</param>
    /// <param name="boldItalic">Optional bold-italic face.</param>
    /// <exception cref="ArgumentException">Empty <paramref name="familyId"/> or duplicate id.</exception>
    public void RegisterFamilyFromBytes(
        string familyId,
        ReadOnlyMemory<byte> regular,
        ReadOnlyMemory<byte>? bold = null,
        ReadOnlyMemory<byte>? italic = null,
        ReadOnlyMemory<byte>? boldItalic = null)
    {
        if (string.IsNullOrEmpty(familyId))
            throw new ArgumentException("Family id is required.", nameof(familyId));

        if (_families.ContainsKey(familyId))
            throw new InvalidOperationException($"Font family '{familyId}' is already registered.");

        var collection = new FontCollection();
        using var rs = OpenRead(regular);
        var regFamily = collection.Add(rs, CultureInfo.InvariantCulture);
        FontFamily? b = null, i = null, bi = null;
        if (bold is { } mb)
        {
            using var s = OpenRead(mb);
            b = collection.Add(s, CultureInfo.InvariantCulture);
        }

        if (italic is { } mi)
        {
            using var s = OpenRead(mi);
            i = collection.Add(s, CultureInfo.InvariantCulture);
        }

        if (boldItalic is { } mbi)
        {
            using var s = OpenRead(mbi);
            bi = collection.Add(s, CultureInfo.InvariantCulture);
        }

        _families[familyId] = new RegisteredFamily(regFamily, b, i, bi, regular, bold, italic, boldItalic);
    }

    /// <summary>
    /// Loads each face from the VFS and registers the family (async IO, synchronous registration).
    /// </summary>
    /// <param name="assets">Asset loader for the layered VFS.</param>
    /// <param name="familyId">Logical family id.</param>
    /// <param name="regularPath">Virtual path to the regular face (required).</param>
    /// <param name="boldPath">Optional bold face path.</param>
    /// <param name="italicPath">Optional italic face path.</param>
    /// <param name="boldItalicPath">Optional bold-italic face path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RegisterFamilyFromVirtualPathsAsync(
        AssetManager assets,
        string familyId,
        string regularPath,
        string? boldPath = null,
        string? italicPath = null,
        string? boldItalicPath = null,
        CancellationToken cancellationToken = default)
    {
        var reg = await assets.LoadBytesAsync(regularPath, cancellationToken).ConfigureAwait(false);
        ReadOnlyMemory<byte>? b = null, i = null, bi = null;
        if (boldPath is { } bp)
            b = await assets.LoadBytesAsync(bp, cancellationToken).ConfigureAwait(false);
        if (italicPath is { } ip)
            i = await assets.LoadBytesAsync(ip, cancellationToken).ConfigureAwait(false);
        if (boldItalicPath is { } bip)
            bi = await assets.LoadBytesAsync(bip, cancellationToken).ConfigureAwait(false);

        RegisterFamilyFromBytes(familyId, reg, b, i, bi);
    }

    /// <summary>Returns whether <paramref name="familyId"/> was registered.</summary>
    public bool TryGetFamily(string familyId, out RegisteredFamily? family)
    {
        if (familyId is null)
        {
            family = null;
            return false;
        }

        return _families.TryGetValue(familyId, out family);
    }

    /// <summary>
    /// Resolve/create a cached font under <see cref="FontRasterSync"/> (caller must hold the lock through any follow-up
    /// SixLabors measure or raster work on <paramref name="font"/>).
    /// </summary>
    internal bool TryCreateFontUnlocked(in TextStyle style, out Font font, out FontFaceKind usedFace)
    {
        font = null!;
        usedFace = FontFaceKind.Regular;
        if (!TryGetFamily(style.FontFamilyId, out var fam))
            return false;

        usedFace = SelectFace(style.Bold, style.Italic, fam!);
        var q = QuantizeEmSizePixels(style.SizePixels);
        var cacheKey = (style.FontFamilyId, usedFace, q);

        if (_measureFontCache.TryGetValue(cacheKey, out var cached))
        {
            font = cached;
            return true;
        }

        var face = fam!.GetFace(usedFace);
        font = CreateFontAtPixelSize(face, EmQuantToPixels(q));
        _measureFontCache[cacheKey] = font;
        return true;
    }

    /// <summary>Convenience for tests and single-threaded callers: locks <see cref="FontRasterSync"/> for lookup only.</summary>
    internal bool TryCreateFont(in TextStyle style, out Font font, out FontFaceKind usedFace)
    {
        lock (_measureFontLock)
            return TryCreateFontUnlocked(in style, out font, out usedFace);
    }

    /// <summary>
    /// OS/2 outline distances scaled to the same quantized em px grid as <see cref="TextGlyphCache"/>.
    /// <see cref="UnderlineCenterDeltaPositiveDownPx"/> / <see cref="StrikethroughCenterDeltaPositiveDownPx"/> are expressed as
    /// **viewport-style +Y-down deltas**: add to baseline Y to reach the strip center (underline delta is non-negative from
    /// OS/2 placement math). World-space submission applies the same magnitudes with the world +Y-up convention.
    /// </summary>
    internal readonly record struct OpenTypeTextDecorationLayout(
        float UnderlineCenterDeltaPositiveDownPx,
        float UnderlineThicknessPx,
        float StrikethroughCenterDeltaPositiveDownPx,
        float StrikethroughThicknessPx)
    {
        /// <summary>
        /// Same vertical convention as <see cref="TextGlyphCache.CachedGlyph.OffsetPenToCenterYWorld"/> (+Y up): negative moves
        /// the strip center below the typographic baseline.
        /// </summary>
        public float UnderlineCenterOffsetPenFontUp => -UnderlineCenterDeltaPositiveDownPx;

        /// <inheritdoc cref="UnderlineCenterOffsetPenFontUp"/>
        public float StrikethroughCenterOffsetPenFontUp => -StrikethroughCenterDeltaPositiveDownPx;
    }

    /// <summary>
    /// Reads OS/2 underline + strikeout tables via SixLabors and converts to pixel deltas that match HUD (+Y down) placement.
    /// </summary>
    internal bool TryGetOpenTypeTextDecorationLayout(in TextStyle style, out OpenTypeTextDecorationLayout layout)
    {
        layout = default;
        lock (_measureFontLock)
        {
            if (!TryCreateFontUnlocked(in style, out var font, out _))
                return false;

            var m = font.FontMetrics;
            var emPx = EmQuantToPixels(QuantizeEmSizePixels(style.SizePixels));
            var scale = emPx / m.UnitsPerEm;

            // Underline: SixLabors exposes OS/2 distance to the top of the stroke from the baseline (negative = typical,
            // below the baseline). Positive values mean "not below" per SixLabors docs — using a signed offset alone can
            // flip strip placement to an overline. Decoration placement always treats the magnitude as distance downward from
            // the baseline to the top edge, then offsets to strip center (same as legacy Abs behavior).
            var uTopDown = MathF.Abs(m.UnderlinePosition * scale);
            var uThick = MathF.Max(0.75f, MathF.Abs(m.UnderlineThickness * scale));
            var underlineCenterDown = uTopDown + uThick * 0.5f;

            // Strikeout: interpret SixLabors strike top relative to baseline in +Y-up font math, then map strip center to +Y-down delta.
            var strikeTopUp = m.StrikeoutPosition * scale;
            var sThick = MathF.Max(0.75f, MathF.Abs(m.StrikeoutSize * scale));
            var strikeCenterUp = strikeTopUp - sThick * 0.5f;
            var strikeCenterDown = -strikeCenterUp;

            layout = new OpenTypeTextDecorationLayout(underlineCenterDown, uThick, strikeCenterDown, sThick);
            return true;
        }
    }

    /// <summary>
    /// Creates a non-cached font instance suitable for background workers.
    /// This avoids sharing SixLabors font objects across threads.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Transient worker font path is validated through async glyph integration tests.")]
    internal bool TryCreateTransientFont(TextStyle style, out Font font, out FontFaceKind usedFace)
    {
        font = null!;
        usedFace = FontFaceKind.Regular;
        if (!TryGetFamily(style.FontFamilyId, out var fam) || fam is null)
            return false;

        usedFace = SelectFace(style.Bold, style.Italic, fam);
        var faceBytes = fam.GetFaceBytes(usedFace);
        using var stream = OpenRead(faceBytes);
        var collection = new FontCollection();
        var loadedFace = collection.Add(stream, CultureInfo.InvariantCulture);
        font = CreateFontAtPixelSize(loadedFace, EmQuantToPixels(QuantizeEmSizePixels(style.SizePixels)));
        return true;
    }

    /// <summary>Rounds nominal pixel size to a stable cache key (1/256 px resolution).</summary>
    internal static int QuantizeEmSizePixels(float sizePixels) =>
        (int)MathF.Round(Math.Clamp(sizePixels, 1f / 256f, 65536f) * 256f);

    /// <summary>Inverse of <see cref="QuantizeEmSizePixels"/> for SixLabors point size mapping.</summary>
    internal static float EmQuantToPixels(int quant) => quant / 256f;

    internal static Font CreateFontAtPixelSize(FontFamily face, float sizePixels)
    {
        // Map nominal pixel em-size to points at 96 DPI (SixLabors layout uses Dpi on TextOptions).
        var points = sizePixels * 72f / 96f;
        return face.CreateFont(Math.Max(points, 0.5f));
    }

    internal static FontFaceKind SelectFace(bool bold, bool italic, RegisteredFamily fam)
    {
        if (bold && italic)
        {
            if (fam.BoldItalic is not null)
                return FontFaceKind.BoldItalic;
            if (fam.Bold is not null)
                return FontFaceKind.Bold;
            if (fam.Italic is not null)
                return FontFaceKind.Italic;
            return FontFaceKind.Regular;
        }

        if (bold && fam.Bold is not null)
            return FontFaceKind.Bold;
        if (italic && fam.Italic is not null)
            return FontFaceKind.Italic;
        return FontFaceKind.Regular;
    }

    private static MemoryStream OpenRead(ReadOnlyMemory<byte> memory) =>
        new(memory.ToArray(), writable: false);

    /// <summary>Holds up to four <see cref="FontFamily"/> references from a <see cref="FontCollection"/>.</summary>
    public sealed class RegisteredFamily
    {
        internal RegisteredFamily(
            FontFamily regular,
            FontFamily? bold,
            FontFamily? italic,
            FontFamily? boldItalic)
            : this(regular, bold, italic, boldItalic, ReadOnlyMemory<byte>.Empty, null, null, null)
        {
        }

        internal RegisteredFamily(
            FontFamily regular,
            FontFamily? bold,
            FontFamily? italic,
            FontFamily? boldItalic,
            ReadOnlyMemory<byte> regularBytes,
            ReadOnlyMemory<byte>? boldBytes,
            ReadOnlyMemory<byte>? italicBytes,
            ReadOnlyMemory<byte>? boldItalicBytes)
        {
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
            RegularBytes = regularBytes;
            BoldBytes = boldBytes;
            ItalicBytes = italicBytes;
            BoldItalicBytes = boldItalicBytes;
        }

        /// <summary>Regular face (always present).</summary>
        public FontFamily Regular { get; }

        /// <summary>Optional bold face.</summary>
        public FontFamily? Bold { get; }

        /// <summary>Optional italic face.</summary>
        public FontFamily? Italic { get; }

        /// <summary>Optional bold-italic face.</summary>
        public FontFamily? BoldItalic { get; }

        [ExcludeFromCodeCoverage(Justification = "Simple DTO accessor.")]
        internal ReadOnlyMemory<byte> RegularBytes { get; }
        [ExcludeFromCodeCoverage(Justification = "Simple DTO accessor.")]
        internal ReadOnlyMemory<byte>? BoldBytes { get; }
        [ExcludeFromCodeCoverage(Justification = "Simple DTO accessor.")]
        internal ReadOnlyMemory<byte>? ItalicBytes { get; }
        [ExcludeFromCodeCoverage(Justification = "Simple DTO accessor.")]
        internal ReadOnlyMemory<byte>? BoldItalicBytes { get; }

        internal FontFamily GetFace(FontFaceKind kind) =>
            kind switch
            {
                FontFaceKind.Bold => Bold ?? Regular,
                FontFaceKind.Italic => Italic ?? Regular,
                FontFaceKind.BoldItalic => BoldItalic ?? Bold ?? Italic ?? Regular,
                _ => Regular
            };

        [ExcludeFromCodeCoverage(Justification = "Fallback mapping is deterministic and exercised indirectly.")]
        internal ReadOnlyMemory<byte> GetFaceBytes(FontFaceKind kind) =>
            kind switch
            {
                FontFaceKind.Bold => BoldBytes ?? RegularBytes,
                FontFaceKind.Italic => ItalicBytes ?? RegularBytes,
                FontFaceKind.BoldItalic => BoldItalicBytes ?? BoldBytes ?? ItalicBytes ?? RegularBytes,
                _ => RegularBytes
            };
    }
}
