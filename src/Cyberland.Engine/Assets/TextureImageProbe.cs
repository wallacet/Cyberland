using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Lightweight PNG/image dimension probes through the VFS without full GPU upload.
/// </summary>
internal static class TextureImageProbe
{
    /// <summary>Returns false when the path is missing, unreadable, or not a supported image.</summary>
    internal static bool TryGetImageDimensions(VirtualFileSystem vfs, string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!vfs.TryOpenRead(path, out var stream))
            return false;

        using (stream)
        {
            try
            {
                var info = Image.Identify(stream);
                width = info?.Width ?? 0;
                height = info?.Height ?? 0;
                return width > 0 && height > 0;
            }
            catch (UnknownImageFormatException)
            {
                return false;
            }
        }
    }
}
