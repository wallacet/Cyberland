using Silk.NET.Maths;

namespace Cyberland.Engine.Rendering.Text;

/// <summary>
/// Font face selection, size, color, and decoration flags for one draw or <see cref="TextRun"/>.
/// </summary>
/// <param name="FontFamilyId">Logical id (e.g. <see cref="BuiltinFonts.UiSans"/>).</param>
/// <param name="SizePixels">Nominal em size in pixels (mapped to SixLabors point size at 96 DPI).</param>
/// <param name="Color">Straight-alpha RGBA multiplier applied to glyph coverage.</param>
/// <param name="Bold">Prefer bold face when the family registered one.</param>
/// <param name="Italic">Prefer italic face when the family registered one.</param>
/// <param name="Underline">Draw an underline segment after glyph quads.</param>
/// <param name="Strikethrough">Draw a strike-through segment after glyph quads.</param>
/// <remarks>
/// <see cref="FontFamilyId"/> must match a family registered on <see cref="FontLibrary"/> (built-in ids from
/// <see cref="BuiltinFonts"/> or mod-registered names). Bold/italic resolve to separate TTF faces when provided;
/// otherwise they fall back to regular metrics.
/// </remarks>
public readonly record struct TextStyle(
    string FontFamilyId,
    float SizePixels,
    Vector4D<float> Color,
    bool Bold = false,
    bool Italic = false,
    bool Underline = false,
    bool Strikethrough = false);
