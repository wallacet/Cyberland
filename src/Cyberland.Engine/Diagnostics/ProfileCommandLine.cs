namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Parses unattended profiling / perf-capture command-line flags.
/// </summary>
public static class ProfileCommandLine
{
    /// <summary>Long-form CLI token for <see cref="TryParseProfileSeconds"/>.</summary>
    public const string SecondsFlag = "--profile-seconds";

    /// <summary>Long-form CLI token for <see cref="TryParseProfileDump"/>.</summary>
    public const string DumpFlag = "--profile-dump";

    /// <summary>Long-form CLI token for <see cref="TryParsePerfDump"/>.</summary>
    public const string PerfDumpFlag = "--perf-dump";

    /// <summary>Long-form CLI token for <see cref="TryParseProfileAlloc"/>.</summary>
    public const string AllocFlag = "--profile-alloc";

    /// <summary>Returns wall-clock profile duration in seconds, or <c>null</c> if profiling is disabled.</summary>
    public static double? TryParseProfileSeconds(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith(SecondsFlag + "=", StringComparison.OrdinalIgnoreCase))
            {
                var tail = a.AsSpan((SecondsFlag + "=").Length);
                if (double.TryParse(tail, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0)
                    return v;
                return null;
            }

            if (a.Equals(SecondsFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v2) && v2 > 0)
                    return v2;
                return null;
            }
        }

        return null;
    }

    /// <summary>Returns dump file path, or <c>null</c> if absent. Release builds ignore dumps; <see cref="GameApplication"/> logs when set.</summary>
    public static string? TryParseProfileDump(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith(DumpFlag + "=", StringComparison.OrdinalIgnoreCase))
                return a[(DumpFlag + "=").Length..].Trim();

            if (a.Equals(DumpFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1].Trim();
        }

        return null;
    }

    /// <summary>Returns perf summary dump file path, or <c>null</c> if absent.</summary>
    public static string? TryParsePerfDump(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith(PerfDumpFlag + "=", StringComparison.OrdinalIgnoreCase))
                return a[(PerfDumpFlag + "=").Length..].Trim();

            if (a.Equals(PerfDumpFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1].Trim();
        }

        return null;
    }

    /// <summary>
    /// When true, debug builds should enable per-scope allocation tracking on <c>FrameProfiler</c> for the session.
    /// Presence of <c>--profile-alloc</c> enables; <c>--profile-alloc=false</c> does not (explicit off for scripts that
    /// forward unknown flags).
    /// </summary>
    public static bool TryParseProfileAlloc(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith(AllocFlag + "=", StringComparison.OrdinalIgnoreCase))
            {
                var tail = a[(AllocFlag + "=").Length..].Trim();
                if (tail.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                    tail.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }

            if (a.Equals(AllocFlag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
