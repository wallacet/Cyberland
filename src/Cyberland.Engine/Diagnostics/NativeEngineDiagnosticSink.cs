using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Host sink: modal dialogs on Windows for Major/Minor (Minor uses warning icon and session dedupe is applied before this sink runs), stderr for Warning, and <see cref="EngineDiagnostics.ReportFatal"/> handled separately.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Calls user32 MessageBoxW and Environment.Exit; stderr paths covered via StderrEngineDiagnosticSink.")]
internal sealed class NativeEngineDiagnosticSink : IEngineDiagnosticSink
{
    internal static NativeEngineDiagnosticSink Instance { get; } = new();

    private NativeEngineDiagnosticSink()
    {
    }

    /// <inheritdoc />
    public void Deliver(EngineErrorSeverity severity, string title, string message)
    {
        switch (severity)
        {
            case EngineErrorSeverity.Major:
                UserMessageDialog.ShowError(title, message);
                break;
            case EngineErrorSeverity.Minor:
                UserMessageDialog.ShowWarning(title, message);
                break;
            case EngineErrorSeverity.Warning:
                UserMessageDialog.WriteDiagnosticToStderr("WARNING", title, message);
                System.Diagnostics.Debug.WriteLine($"[WARNING] {title}: {message}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Only Major, Minor, and Warning are supported.");
        }
    }
}
