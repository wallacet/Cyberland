using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Cyberland.Engine.Rendering;

/// <summary>Shows blocking dialogs for engine diagnostics; uses native message boxes on Windows, otherwise stderr.</summary>
public static class UserMessageDialog
{
    private const uint MbOk = 0;
    private const uint MbIconError = 0x00000010;
    private const uint MbIconWarning = 0x00000030;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint MessageBoxW(nint hWnd, string? text, string? caption, uint type);

    /// <summary>Native message box path (not unit-tested). Falls back to <see cref="WriteErrorToStderr"/> on failure.</summary>
    /// <param name="title">Short caption for the dialog or stderr header.</param>
    /// <param name="message">Body text (may be multi-line).</param>
    [ExcludeFromCodeCoverage(Justification = "Calls user32 MessageBoxW; stderr branch is covered via WriteErrorToStderr.")]
    public static void ShowError(string title, string message)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                MessageBoxW(0, message, title, MbOk | MbIconError);
                return;
            }
            catch
            {
                // Fall through to console.
            }
        }

        WriteErrorToStderr(title, message);
    }

    /// <summary>Warning-styled message box on Windows; otherwise stderr with a WARNING prefix.</summary>
    /// <param name="title">Short caption for the dialog or stderr header.</param>
    /// <param name="message">Body text (may be multi-line).</param>
    [ExcludeFromCodeCoverage(Justification = "Calls user32 MessageBoxW; stderr branch is covered via WriteWarningToStderr.")]
    public static void ShowWarning(string title, string message)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                MessageBoxW(0, message, title, MbOk | MbIconWarning);
                return;
            }
            catch
            {
                // Fall through to console.
            }
        }

        WriteWarningToStderr(title, message);
    }

    internal static void WriteErrorToStderr(string title, string message)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== " + title + " ===");
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
    }

    internal static void WriteWarningToStderr(string title, string message)
    {
        WriteDiagnosticToStderr("WARNING", title, message);
    }

    /// <summary>Structured stderr block for <see cref="Cyberland.Engine.Diagnostics.EngineDiagnostics"/> (fully testable).</summary>
    internal static void WriteDiagnosticToStderr(string severityLabel, string title, string message)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"[{severityLabel}] === {title} ===");
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
    }
}
