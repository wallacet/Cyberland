using System.Globalization;
using Cyberland.Engine.Assets;
using SixLabors.Fonts;

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

        _families[familyId] = new RegisteredFamily(regFamily, b, i, bi);
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

    internal bool TryCreateFont(in TextStyle style, out Font font, out FontFaceKind usedFace)
    {
        font = null!;
        usedFace = FontFaceKind.Regular;
        if (!TryGetFamily(style.FontFamilyId, out var fam))
            return false;

        usedFace = SelectFace(style.Bold, style.Italic, fam!);
        var face = fam!.GetFace(usedFace);
        var q = QuantizeEmSizePixels(style.SizePixels);
        font = CreateFontAtPixelSize(face, EmQuantToPixels(q));
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
        {
            Regular = regular;
            Bold = bold;
            Italic = italic;
            BoldItalic = boldItalic;
        }

        /// <summary>Regular face (always present).</summary>
        public FontFamily Regular { get; }

        /// <summary>Optional bold face.</summary>
        public FontFamily? Bold { get; }

        /// <summary>Optional italic face.</summary>
        public FontFamily? Italic { get; }

        /// <summary>Optional bold-italic face.</summary>
        public FontFamily? BoldItalic { get; }

        internal FontFamily GetFace(FontFaceKind kind) =>
            kind switch
            {
                FontFaceKind.Bold => Bold ?? Regular,
                FontFaceKind.Italic => Italic ?? Regular,
                FontFaceKind.BoldItalic => BoldItalic ?? Bold ?? Italic ?? Regular,
                _ => Regular
            };
    }
}
