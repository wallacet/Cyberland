using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.Maths;

namespace Cyberland.Engine.Assets;

/// <summary>JSON schema for gameplay sprite atlas manifests (version 1).</summary>
internal sealed class SpriteAtlasManifestDto
{
    public int SchemaVersion { get; init; } = 1;
    public SpriteAtlasPageDto[] Pages { get; init; } = Array.Empty<SpriteAtlasPageDto>();
    public SpriteAtlasRegionDto[] Regions { get; init; } = Array.Empty<SpriteAtlasRegionDto>();
    public Dictionary<string, SpriteAtlasAnimationDto>? Animations { get; init; }
    public Dictionary<string, SpriteAtlasSheetDto>? Sheets { get; init; }
}

internal sealed class SpriteAtlasPageDto
{
    public string Path { get; init; } = "";
}

internal sealed class SpriteAtlasRegionDto
{
    public string Name { get; init; } = "";
    public int PageIndex { get; init; }
    public int[] PixelRect { get; init; } = Array.Empty<int>();
    public float[]? Pivot { get; init; }
    public float[]? SizeWorld { get; init; }
    public int[]? NineSlice { get; init; }
}

internal sealed class SpriteAtlasAnimationDto
{
    public string[] RegionNames { get; init; } = Array.Empty<string>();
    public float SecondsPerFrame { get; init; }
    public bool Loop { get; init; } = true;
}

internal sealed class SpriteAtlasSheetDto
{
    public string RegionName { get; init; } = "";
    public int Columns { get; init; }
    public int FrameCount { get; init; }
    public float SecondsPerFrame { get; init; }
    public bool Loop { get; init; } = true;
}

/// <summary>
/// Parsed atlas animation clip (frame list + timing).
/// </summary>
public sealed class SpriteAtlasAnimationClip
{
    /// <summary>Ordered region names for each frame.</summary>
    public required string[] RegionNames { get; init; }

    /// <summary>Duration of one frame in seconds.</summary>
    public float SecondsPerFrame { get; init; }

    /// <summary>Whether playback wraps to frame zero.</summary>
    public bool Loop { get; init; } = true;
}

/// <summary>
/// Uniform-grid flipbook defined inside one atlas region.
/// </summary>
public sealed class SpriteAtlasSheetClip
{
    /// <summary>Base region covering the full sheet image.</summary>
    public required string RegionName { get; init; }

    /// <summary>Column count in the grid.</summary>
    public int Columns { get; init; }

    /// <summary>Total frames in the clip.</summary>
    public int FrameCount { get; init; }

    /// <summary>Duration of one frame in seconds.</summary>
    public float SecondsPerFrame { get; init; }

    /// <summary>Whether playback wraps.</summary>
    public bool Loop { get; init; } = true;
}

/// <summary>
/// One named sub-rectangle within a loaded atlas page.
/// </summary>
public sealed class SpriteAtlasRegion
{
    /// <summary>Logical region name (unique within the manifest).</summary>
    public required string Name { get; init; }

    /// <summary>GPU texture slot for the page containing this region.</summary>
    public TextureId PageTextureId { get; init; }

    /// <summary>Page index from the manifest.</summary>
    public int PageIndex { get; init; }

    /// <summary>Region origin and size in page pixels.</summary>
    public int PixelX { get; init; }
    /// <summary>Region origin Y in page pixels.</summary>
    public int PixelY { get; init; }
    /// <summary>Region width in page pixels.</summary>
    public int PixelWidth { get; init; }
    /// <summary>Region height in page pixels.</summary>
    public int PixelHeight { get; init; }

    /// <summary>Normalized UV rect (min.xy, max.zw) within the page texture.</summary>
    public Vector4D<float> UvRect { get; init; }

    /// <summary>Pivot in normalized region space (0–1).</summary>
    public Vector2D<float> Pivot { get; init; } = new(0.5f, 0.5f);

    /// <summary>Default half-extents in world units when bound to a sprite.</summary>
    public Vector2D<float> HalfExtentsWorld { get; init; }

    /// <summary>Optional nine-slice insets in source pixels.</summary>
    public NineSliceInsets NineSlice { get; init; }
}

/// <summary>
/// Immutable loaded atlas: regions, animations, and sheets resolved for one culture + manifest path.
/// </summary>
public sealed class SpriteAtlas
{
    /// <summary>Authoring path passed to the catalog (never a <c>Locale/…</c> prefix).</summary>
    public string CanonicalManifestPath { get; init; } = "";

    /// <summary>VFS path of the manifest JSON that was loaded.</summary>
    public string ResolvedManifestPath { get; init; } = "";

    /// <summary>Primary culture used when this atlas was loaded.</summary>
    public string CultureName { get; init; } = "";

    /// <summary>Page width/height in pixels (index matches manifest page list).</summary>
    public int[] PageWidths { get; init; } = Array.Empty<int>();

    /// <summary>Page height in pixels.</summary>
    public int[] PageHeights { get; init; } = Array.Empty<int>();

    private readonly Dictionary<string, SpriteAtlasRegion> _regions;
    private readonly Dictionary<string, SpriteAtlasAnimationClip> _animations;
    private readonly Dictionary<string, SpriteAtlasSheetClip> _sheets;

    internal SpriteAtlas(
        string canonicalManifestPath,
        string resolvedManifestPath,
        string cultureName,
        int[] pageWidths,
        int[] pageHeights,
        Dictionary<string, SpriteAtlasRegion> regions,
        Dictionary<string, SpriteAtlasAnimationClip> animations,
        Dictionary<string, SpriteAtlasSheetClip> sheets)
    {
        CanonicalManifestPath = canonicalManifestPath;
        ResolvedManifestPath = resolvedManifestPath;
        CultureName = cultureName;
        PageWidths = pageWidths;
        PageHeights = pageHeights;
        _regions = regions;
        _animations = animations;
        _sheets = sheets;
    }

    /// <summary>Looks up a named region.</summary>
    public bool TryGetRegion(string name, out SpriteAtlasRegion region) =>
        _regions.TryGetValue(name, out region!);

    /// <summary>Looks up a named frame-list animation.</summary>
    public bool TryGetAnimation(string name, out SpriteAtlasAnimationClip clip) =>
        _animations.TryGetValue(name, out clip!);

    /// <summary>Looks up a named uniform-grid sheet clip.</summary>
    public bool TryGetSheet(string name, out SpriteAtlasSheetClip sheet) =>
        _sheets.TryGetValue(name, out sheet!);
}

/// <summary>Parses sprite atlas manifest JSON into runtime structures.</summary>
public static class SpriteAtlasManifestParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Deserializes manifest JSON bytes.</summary>
    internal static SpriteAtlasManifestDto Parse(ReadOnlySpan<byte> utf8Json) =>
        JsonSerializer.Deserialize<SpriteAtlasManifestDto>(utf8Json, JsonOptions)
        ?? throw new JsonException("Sprite atlas manifest deserialized to null.");

    /// <summary>
    /// Computes normalized UV coordinates for a sub-rectangle within a page texture.
    /// </summary>
    public static Vector4D<float> PixelRectToUvRect(int px, int py, int pw, int ph, int pageWidth, int pageHeight)
    {
        if (pageWidth <= 0 || pageHeight <= 0 || pw <= 0 || ph <= 0)
            return new Vector4D<float>(0f, 0f, 1f, 1f);
        var u0 = px / (float)pageWidth;
        var v0 = py / (float)pageHeight;
        var u1 = (px + pw) / (float)pageWidth;
        var v1 = (py + ph) / (float)pageHeight;
        return new Vector4D<float>(u0, v0, u1, v1);
    }

    /// <summary>
    /// Subdivides a sheet region UV rect into a uniform grid frame.
    /// </summary>
    public static Vector4D<float> SheetFrameUvRect(in Vector4D<float> sheetUv, int columns, int frameIndex, int frameCount)
    {
        if (columns <= 0 || frameCount <= 0)
            return sheetUv;
        var rows = (frameCount + columns - 1) / columns;
        var frame = Math.Clamp(frameIndex, 0, frameCount - 1);
        var fx = frame % columns;
        var fy = frame / columns;
        var uw = (sheetUv.Z - sheetUv.X) / columns;
        var uh = (sheetUv.W - sheetUv.Y) / rows;
        var u0 = sheetUv.X + fx * uw;
        var v0 = sheetUv.Y + fy * uh;
        return new Vector4D<float>(u0, v0, u0 + uw, v0 + uh);
    }
}
