using System.Text.Json.Serialization;
using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// JSON schema for pre-baked MSDF atlas data (manifest + referenced PNG pages).
/// </summary>
internal sealed class BakedMsdfAtlasManifest
{
    public int Version { get; init; } = 1;
    public string FamilyId { get; init; } = "";
    public string Face { get; init; } = "Regular";
    public float SizePixels { get; init; }
    public int RasterRevision { get; init; }
    public int PageSizePixels { get; init; } = 2048;
    public BakedMsdfAtlasPageRef[] Pages { get; init; } = Array.Empty<BakedMsdfAtlasPageRef>();
    public BakedMsdfGlyphEntry[] Glyphs { get; init; } = Array.Empty<BakedMsdfGlyphEntry>();
}

internal sealed class BakedMsdfAtlasPageRef
{
    public string Path { get; init; } = "";
}

internal sealed class BakedMsdfGlyphEntry
{
    public int CodePoint { get; init; }
    public int PageIndex { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public float DrawWidthPx { get; init; }
    public float DrawHeightPx { get; init; }
    public float OffsetPenToCenterX { get; init; }
    public float OffsetPenToCenterYWorld { get; init; }
    public float AdvancePx { get; init; }
    public float MsdfPixelRange { get; init; }

    [JsonIgnore]
    public Vector4D<float> UvRect { get; set; }
}
