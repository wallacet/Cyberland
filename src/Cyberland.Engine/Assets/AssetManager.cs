using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Async asset IO with optional memory budget for streaming large files in chunks.
/// </summary>
public sealed class AssetManager
{
    private readonly VirtualFileSystem _vfs;

    /// <summary>Creates a loader that reads through <paramref name="vfs"/> (layered mod content).</summary>
    public AssetManager(VirtualFileSystem vfs) => _vfs = vfs;

    /// <summary>Underlying layered filesystem (mods + base).</summary>
    public VirtualFileSystem FileSystem => _vfs;

    /// <summary>Load entire file into memory (small assets, JSON, shaders).</summary>
    public async Task<byte[]> LoadBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_vfs.TryOpenRead(path, out var stream))
            throw new FileNotFoundException(path);

        await using (stream)
        {
            using var ms = new MemoryStream((int)Math.Min(stream.Length, int.MaxValue));
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Reads the file synchronously. Prefer <see cref="LoadBytesAsync"/> in async call chains; use this for hot paths that must
    /// stay on the current thread (for example <see cref="Rendering.IRenderer.RegisterTextureRgba"/> on the window thread).
    /// </summary>
    public byte[] LoadBytes(string path)
    {
        if (!_vfs.TryOpenRead(path, out var stream))
            throw new FileNotFoundException(path);

        using (stream)
        {
            using var ms = new MemoryStream((int)Math.Min(stream.Length, int.MaxValue));
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    /// <summary>Load a texture from the VFS into the renderer on the calling thread.</summary>
    public TextureId LoadTexture(string path, IRenderer renderer)
    {
        var bytes = LoadBytes(path);
        using var image = Image.Load<Rgba32>(bytes);
        var rgba = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(rgba);
        return renderer.RegisterTextureRgba(rgba, image.Width, image.Height);
    }

    /// <summary>Loads UTF-8 text (JSON, shaders, locale files).</summary>
    public async Task<string> LoadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await LoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Deserializes JSON from the VFS into <typeparamref name="T"/>.</summary>
    public async Task<T> LoadJsonAsync<T>(string path, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
    {
        await using var stream = OpenReadOrThrow(path);
        return (await JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>Open a stream for manual chunked reads (audio/video/large textures).</summary>
    public Stream OpenReadOrThrow(string path)
    {
        if (!_vfs.TryOpenRead(path, out var stream))
            throw new FileNotFoundException(path);

        return stream;
    }
}
