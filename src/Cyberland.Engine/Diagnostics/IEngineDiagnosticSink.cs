namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Delivers <see cref="EngineDiagnostics"/> reports after severity routing and Minor deduplication. The stock host replaces the default stderr sink with a native implementation after the window exists.
/// </summary>
public interface IEngineDiagnosticSink
{
    /// <summary>
    /// Invoked for <see cref="EngineErrorSeverity.Major"/>, <see cref="EngineErrorSeverity.Minor"/>, or <see cref="EngineErrorSeverity.Warning"/> only.
    /// </summary>
    void Deliver(EngineErrorSeverity severity, string title, string message);
}
