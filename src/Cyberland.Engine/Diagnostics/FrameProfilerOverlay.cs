using System.Text;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Thin wrapper so HUD code can show <see cref="FrameProfiler"/> text without duplicating sort rules.
/// In Release builds <see cref="AppendHud"/> is a no-op (profiler data is not collected).
/// </summary>
public static class FrameProfilerOverlay
{
    /// <summary>Default line count for an on-screen profiler panel.</summary>
    public const int DefaultMaxLines = 12;

    /// <summary>Appends top scopes by average CPU time (newest session ordering from <see cref="FrameProfiler.AppendTopScopes"/>).</summary>
    /// <param name="sb">Destination buffer.</param>
    /// <param name="maxLines">Maximum number of scope lines.</param>
    public static void AppendHud(StringBuilder sb, int maxLines = DefaultMaxLines)
    {
#if DEBUG
        FrameProfiler.AppendTopScopes(sb, maxLines);
#else
        _ = sb;
        _ = maxLines;
#endif
    }
}
