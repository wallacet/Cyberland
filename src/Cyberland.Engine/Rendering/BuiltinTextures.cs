namespace Cyberland.Engine.Rendering;

/// <summary>
/// Procedurally generated RGBA8 payloads for engine-owned placeholder textures (no VFS or GPU required).
/// </summary>
public static class BuiltinTextures
{
    /// <summary>Default width/height for <see cref="IRenderer.MissingTextureId"/>.</summary>
    public const int MissingTextureSize = 64;

    /// <summary>Checker tile size in pixels for the missing-texture pattern.</summary>
    public const int MissingTextureTileSize = 8;

    /// <summary>
    /// Builds a magenta/green checkerboard RGBA8 buffer (sRGB intent) used when content fails to load.
    /// </summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="tileSize">Checker tile edge length in pixels.</param>
    public static byte[] CreateMissingTextureRgba(int width, int height, int tileSize)
    {
        if (width <= 0 || height <= 0 || tileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Missing texture dimensions and tile size must be positive.");

        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var magenta = ((x / tileSize) + (y / tileSize)) % 2 == 0;
                var idx = (y * width + x) * 4;
                rgba[idx] = magenta ? (byte)255 : (byte)0;
                rgba[idx + 1] = magenta ? (byte)0 : (byte)255;
                rgba[idx + 2] = magenta ? (byte)255 : (byte)0;
                rgba[idx + 3] = 255;
            }
        }

        return rgba;
    }
}
