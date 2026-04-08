using System.Text.Json;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Async asset IO with optional memory budget for streaming large files in chunks.
/// </summary>
public sealed class AssetManager
{
    private readonly VirtualFileSystem _vfs;

    public AssetManager(VirtualFileSystem vfs) => _vfs = vfs;

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

    public async Task<string> LoadTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await LoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

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
