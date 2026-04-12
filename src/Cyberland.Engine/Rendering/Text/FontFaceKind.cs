namespace Cyberland.Engine.Rendering.Text;

/// <summary>Which face from a registered family to use when rasterizing and measuring.</summary>
internal enum FontFaceKind : byte
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    BoldItalic = 3
}
