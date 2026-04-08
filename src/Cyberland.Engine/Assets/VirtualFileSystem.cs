namespace Cyberland.Engine.Assets;

/// <summary>
/// Layered read-only view: later roots shadow earlier ones (mods override base content).
/// Paths use forward slashes and are normalized to lowercase for case-insensitive lookups on Windows.
/// </summary>
public sealed class VirtualFileSystem
{
    private readonly List<string> _roots = new();

    public IReadOnlyList<string> Roots => _roots;

    /// <summary>Add a directory root; last added wins when resolving duplicates.</summary>
    public void Mount(string absoluteDirectory)
    {
        var full = Path.GetFullPath(absoluteDirectory);
        if (!Directory.Exists(full))
            return;

        _roots.Add(full);
    }

    public void Clear() => _roots.Clear();

    public bool TryOpenRead(string relativePath, [NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        var rel = Normalize(relativePath);

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
