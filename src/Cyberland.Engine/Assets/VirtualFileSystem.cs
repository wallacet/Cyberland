namespace Cyberland.Engine.Assets;

/// <summary>
/// Layered read-only view: later roots shadow earlier ones (mods override base content).
/// Paths use forward slashes and are normalized to lowercase for case-insensitive lookups on Windows.
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly List<string> _roots = new();
    private readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Mounted directories in add order; last entry wins when resolving duplicates.</summary>
    public IReadOnlyList<string> Roots => _roots;

    /// <summary>
    /// Add a directory root; last added wins when resolving duplicates.
    /// A mount whose full path equals the most recently added root is ignored (same resolution order; avoids duplicate scans when the mod loader and <see cref="Cyberland.Engine.Modding.ModLoadContext.MountDefaultContent"/> both mount the same folder).
    /// </summary>
    public void Mount(string absoluteDirectory)
    {
        var full = Path.GetFullPath(absoluteDirectory);
        if (!Directory.Exists(full))
            return;

        if (_roots.Count > 0 && string.Equals(_roots[^1], full, StringComparison.OrdinalIgnoreCase))
            return;

        _roots.Add(full);
    }

    /// <summary>Removes all mounts and block entries (tests / tooling).</summary>
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

    /// <summary>Opens the first matching file from the newest mount, unless the path is blocked.</summary>
    /// <param name="relativePath">Virtual path using forward slashes, case-insensitive.</param>
    /// <param name="stream">Opened read stream when this method returns <see langword="true"/>.</param>
    public bool TryOpenRead(string relativePath, [NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        var rel = Normalize(relativePath);
        if (rel.Length > 0 && _blocked.Contains(rel))
            return false;

        // One OS path segment per resolve; avoid per-root string Replace allocations.
        var relOs = rel.Replace('/', Path.DirectorySeparatorChar);

        // Last mount wins: iterate backwards.
        for (var i = _roots.Count - 1; i >= 0; i--)
        {
            var candidate = Path.Combine(_roots[i], relOs);
            if (!File.Exists(candidate))
                continue;

            stream = File.OpenRead(candidate);
            return true;
        }

        return false;
    }

    /// <summary>True if any mount exposes a file at the normalized path and it is not blocked.</summary>
    public bool Exists(string relativePath)
    {
        var rel = Normalize(relativePath);
        if (rel.Length > 0 && _blocked.Contains(rel))
            return false;

        var relOs = rel.Replace('/', Path.DirectorySeparatorChar);
        for (var i = _roots.Count - 1; i >= 0; i--)
        {
            var candidate = Path.Combine(_roots[i], relOs);
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
