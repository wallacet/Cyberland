using System.Text;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Builds readable text for fatal dialogs and stderr from arbitrary exception objects (including non-<see cref="Exception"/> payloads).
/// </summary>
public static class UnhandledExceptionFormatter
{
    /// <summary>
    /// Formats <paramref name="exceptionObject"/> for display, optionally truncating the result.
    /// </summary>
    public static string FormatExceptionForDisplay(object exceptionObject, int maxLength = 12_000)
    {
        if (maxLength < 64)
            maxLength = 64;

        var sb = new StringBuilder();
        if (exceptionObject is Exception ex)
            AppendExceptionChain(sb, ex);
        else
            sb.Append(exceptionObject?.ToString() ?? "(null)");

        var s = sb.ToString();
        if (s.Length <= maxLength)
            return s;

        return s.AsSpan(0, maxLength - 20).ToString() + "\n… (truncated)";
    }

    private static void AppendExceptionChain(StringBuilder sb, Exception ex)
    {
        var depth = 0;
        for (var e = ex; e != null && depth < 32; e = e.InnerException, depth++)
        {
            if (depth > 0)
                sb.AppendLine().AppendLine("--- Inner exception ---");

            sb.Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            if (!string.IsNullOrEmpty(e.StackTrace))
                sb.AppendLine(e.StackTrace);
        }
    }
}
