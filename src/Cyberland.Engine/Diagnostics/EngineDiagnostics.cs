using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Central reporting API for engine and mod code. The stock host switches from stderr to native message boxes via <see cref="UseNativeUserNotifications"/> after the window is created.
/// </summary>
/// <remarks>
/// Safe to call from <see cref="Core.Ecs.IParallelSystem"/> workers: delivery is serialized with a lock so native dialogs do not interleave. Prefer short titles and avoid per-frame reports for Minor.
/// </remarks>
public static class EngineDiagnostics
{
    private static readonly object Sync = new();
    private static readonly HashSet<string> MinorDedupeKeys = new(StringComparer.Ordinal);
    private static IEngineDiagnosticSink _sink = StderrEngineDiagnosticSink.Instance;

    /// <summary>
    /// Optional sink used instead of the active built-in sink (e.g. unit tests). When null, <see cref="StderrEngineDiagnosticSink"/> or the native sink set by <see cref="UseNativeUserNotifications"/> is used.
    /// </summary>
    public static IEngineDiagnosticSink? SinkOverride { get; set; }

    /// <summary>
    /// Clears <see cref="EngineErrorSeverity.Minor"/> deduplication state for the current process. The stock host does not call this; unit tests may call it between cases so identical Minor messages are reported again.
    /// </summary>
    public static void ClearMinorDedupe()
    {
        lock (Sync)
            MinorDedupeKeys.Clear();
    }

    /// <summary>
    /// Switches delivery to native message boxes for Major/Minor (and stderr for Warning), matching the stock Cyberland host. Idempotent.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Assigns native sink; behavior covered indirectly via stderr default in tests.")]
    public static void UseNativeUserNotifications()
    {
        lock (Sync)
            _sink = NativeEngineDiagnosticSink.Instance;
    }

    /// <summary>
    /// Reports a non-fatal issue. Use <see cref="ReportFatal"/> for <see cref="EngineErrorSeverity.Fatal"/>.
    /// </summary>
    /// <param name="severity">Must be <see cref="EngineErrorSeverity.Major"/>, <see cref="EngineErrorSeverity.Minor"/>, or <see cref="EngineErrorSeverity.Warning"/>.</param>
    /// <param name="title">Short heading for dialogs or stderr blocks.</param>
    /// <param name="message">Human-readable detail (may be multi-line).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="severity"/> is <see cref="EngineErrorSeverity.Fatal"/> or out of range.</exception>
    public static void Report(EngineErrorSeverity severity, string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);
        if (severity is EngineErrorSeverity.Fatal)
            throw new ArgumentOutOfRangeException(nameof(severity), "Fatal severity is not supported here; use ReportFatal(string, string) instead.");

        if (severity is not (EngineErrorSeverity.Major or EngineErrorSeverity.Minor or EngineErrorSeverity.Warning))
            throw new ArgumentOutOfRangeException(nameof(severity));

        lock (Sync)
        {
            if (severity == EngineErrorSeverity.Minor)
            {
                var key = title + "\u241f" + message;
                if (!MinorDedupeKeys.Add(key))
                    return;
            }

            var target = SinkOverride ?? _sink;
            target.Deliver(severity, title, message);
        }
    }

    /// <summary>
    /// Reports a critical failure, shows a blocking error dialog when native notifications are enabled, then terminates the process with a non-zero exit code.
    /// </summary>
    /// <param name="title">Short heading for the fatal dialog.</param>
    /// <param name="message">Human-readable detail (may be multi-line).</param>
    [ExcludeFromCodeCoverage(Justification = "Terminates the process; not executed in unit tests.")]
    public static void ReportFatal(string title, string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(message);
        lock (Sync)
            NativeFatal.Deliver(title, message);
    }
}
