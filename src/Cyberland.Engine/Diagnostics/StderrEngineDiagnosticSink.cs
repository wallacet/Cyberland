using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Default sink before the host enables native notifications: formats severity and writes to stderr (no modal dialogs).
/// </summary>
public sealed class StderrEngineDiagnosticSink : IEngineDiagnosticSink
{
    /// <summary>Shared instance used as the process default until <see cref="EngineDiagnostics.UseNativeUserNotifications"/> runs.</summary>
    public static StderrEngineDiagnosticSink Instance { get; } = new();

    private StderrEngineDiagnosticSink()
    {
    }

    /// <inheritdoc />
    public void Deliver(EngineErrorSeverity severity, string title, string message)
    {
        switch (severity)
        {
            case EngineErrorSeverity.Major:
                UserMessageDialog.WriteDiagnosticToStderr("MAJOR", title, message);
                break;
            case EngineErrorSeverity.Minor:
                UserMessageDialog.WriteDiagnosticToStderr("MINOR", title, message);
                break;
            case EngineErrorSeverity.Warning:
                UserMessageDialog.WriteDiagnosticToStderr("WARNING", title, message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "Only Major, Minor, and Warning are supported.");
        }
    }
}
