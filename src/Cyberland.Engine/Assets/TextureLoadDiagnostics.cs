using System.Collections.Concurrent;

namespace Cyberland.Engine.Assets;

/// <summary>
/// Rate-limited stderr logging for texture load failures so missing content is visible without spamming every frame.
/// </summary>
internal static class TextureLoadDiagnostics
{
    private static readonly ConcurrentDictionary<string, byte> LoggedPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Logs once per normalized VFS path per process.</summary>
    public static void LogFailureOnce(string path, TextureLoadStatus status)
    {
        if (!LoggedPaths.TryAdd(Normalize(path), 0))
            return;
        Console.Error.WriteLine($"[Cyberland.Engine] Texture load failed ({status}): {path}");
    }

    private static string Normalize(string path) =>
        path.Trim().Replace('\\', '/').TrimStart('/');
}
