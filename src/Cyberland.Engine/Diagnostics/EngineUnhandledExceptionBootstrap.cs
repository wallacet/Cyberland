using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Diagnostics;

/// <summary>
/// Registers process-wide handlers so uncaught failures surface like other engine errors. Call once from the host entry before <see cref="GameApplication.Run"/>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Registers AppDomain/TaskScheduler handlers; uses MessageBox and Environment.Exit.")]
public static class EngineUnhandledExceptionBootstrap
{
    private static bool _installed;

    /// <summary>Subscribes to unhandled and unobserved exceptions. Safe to call multiple times (second call is a no-op).</summary>
    public static void Install()
    {
        if (_installed)
            return;
        _installed = true;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var body = UnhandledExceptionFormatter.FormatExceptionForDisplay(e.ExceptionObject);
        UserMessageDialog.ShowError("Cyberland — Unhandled exception", body);
        Environment.Exit(1);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var body = UnhandledExceptionFormatter.FormatExceptionForDisplay(e.Exception);
        EngineDiagnostics.Report(EngineErrorSeverity.Warning, "Cyberland — Unobserved task exception", body);
        e.SetObserved();
    }
}
