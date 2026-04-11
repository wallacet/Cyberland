namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Severity for engine-reported issues. Used by <see cref="EngineDiagnostics"/> to choose user surfacing (dialogs vs stderr) and recovery expectations.
/// </summary>
/// <remarks>
/// Mods and engine systems may report from parallel workers; <see cref="EngineDiagnostics"/> serializes delivery. Prefer concise messages and avoid per-frame spam.
/// </remarks>
public enum EngineErrorSeverity
{
    /// <summary>
    /// Critical failure: the process should terminate after surfacing (see <see cref="EngineDiagnostics.ReportFatal"/>). Use when continuing would corrupt state or mislead the player.
    /// </summary>
    Fatal,

    /// <summary>
    /// Important problem that should be obvious to the player (e.g. failed to apply required graphics settings). The game may continue if recovery is possible.
    /// </summary>
    Major,

    /// <summary>
    /// User-visible degradation that may be subtle (e.g. visible pop-in). Surfacing is deduplicated per distinct message for the process when using native dialogs.
    /// </summary>
    Minor,

    /// <summary>
    /// Development-oriented detail; should not affect end users under normal builds. Logged to stderr (and debug output) rather than modal dialogs in the stock host.
    /// </summary>
    Warning,
}
