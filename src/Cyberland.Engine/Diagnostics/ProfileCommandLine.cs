namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Parses <c>--profile-seconds=N</c> and <c>--profile-dump=path</c> for unattended CPU profiling runs.
/// </summary>
public static class ProfileCommandLine
{
    /// <summary>Long-form CLI token for <see cref="TryParseProfileSeconds"/>.</summary>
    public const string SecondsFlag = "--profile-seconds";

    /// <summary>Long-form CLI token for <see cref="TryParseProfileDump"/>.</summary>
    public const string DumpFlag = "--profile-dump";

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

    /// <summary>Returns dump file path, or <c>null</c> if absent.</summary>
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
}
