namespace Cyberland.Engine.Assets;

/// <summary>
/// Layered read-only view: later roots shadow earlier ones (mods override base content).
/// Paths use forward slashes and are normalized to lowercase for case-insensitive lookups on Windows.
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly List<string> _roots = new();
    private readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Roots => _roots;

    /// <summary>Add a directory root; last added wins when resolving duplicates.</summary>
    public void Mount(string absoluteDirectory)
    {
        var full = Path.GetFullPath(absoluteDirectory);
        if (!Directory.Exists(full))
            return;

        _roots.Add(full);
    }

    public void Clear()
    {
        _roots.Clear();
        _blocked.Clear();
    }

    /// <summary>
    /// Hides a relative path from resolution: <see cref="TryOpenRead"/> and <see cref="Exists"/> return false
    /// even if an earlier mount would have provided the file. Block wins over all mounts (use a higher mount to
    /// override content; use block to suppress).
    /// </summary>
    public void BlockPath(string relativePath)
    {
        var rel = Normalize(relativePath);
        if (rel.Length > 0)
            _blocked.Add(rel);
    }

    /// <summary>Removes a path from the block list if present.</summary>
    public bool UnblockPath(string relativePath)
    {
        var rel = Normalize(relativePath);
        return rel.Length > 0 && _blocked.Remove(rel);
    }

    public bool TryOpenRead(string relativePath, [NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        var rel = Normalize(relativePath);
        if (rel.Length > 0 && _blocked.Contains(rel))
            return false;

        // Last mount wins: iterate backwards.
        for (var i = _roots.Count - 1; i >= 0; i--)
        {
            var candidate = Path.Combine(_roots[i], rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(candidate))
                continue;

            stream = File.OpenRead(candidate);
            return true;
        }

        return false;
    }

    public bool Exists(string relativePath)
    {
        var rel = Normalize(relativePath);
        if (rel.Length > 0 && _blocked.Contains(rel))
            return false;

        for (var i = _roots.Count - 1; i >= 0; i--)
        {
            var candidate = Path.Combine(_roots[i], rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return true;
        }

        return false;
    }

    private static string Normalize(string relativePath)
    {
        var trimmed = relativePath.Trim().Replace('\\', '/');
        return trimmed.TrimStart('/');
    }
}
