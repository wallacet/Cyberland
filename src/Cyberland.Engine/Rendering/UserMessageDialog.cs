using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Cyberland.Engine.Rendering;

/// <summary>Shows blocking dialogs for engine diagnostics; uses native message boxes on Windows, otherwise stderr.</summary>
public static class UserMessageDialog
{
    private const uint MbOk = 0;
    private const uint MbIconError = 0x00000010;
    private const uint MbIconWarning = 0x00000030;
    private const uint CfUnicodetext = 13;
    /// <summary>GMEM_MOVEABLE (0x2) | GMEM_ZEROINIT (0x40).</summary>
    private const uint Ghnd = 0x0042;

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
                var forClipboard = BuildClipboardPayload(title, message);
                _ = TryCopyStringToWindowsClipboard(forClipboard);
                var shown = message
                    + "\r\n\r\nThe full text was copied to the clipboard. Paste (Ctrl+V) to share reports.";
                MessageBoxW(0, shown, title, MbOk | MbIconError);
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
                _ = TryCopyStringToWindowsClipboard(BuildClipboardPayload(title, message));
                var shown = message
                    + "\r\n\r\nThe full text was copied to the clipboard. Paste (Ctrl+V) to share reports.";
                MessageBoxW(0, shown, title, MbOk | MbIconWarning);
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

    [ExcludeFromCodeCoverage(Justification = "Trivial string composition; only consumed from [ExcludeFromCodeCoverage] ShowError/ShowWarning on Windows.")]
    private static string BuildClipboardPayload(string title, string message) =>
        title + "\r\n" + new string('=', title.Length) + "\r\n\r\n" + message;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint hMem);

    [ExcludeFromCodeCoverage(Justification = "P/Invoke clipboard (Windows only); not exercised in test runs.")]
    private static bool TryCopyStringToWindowsClipboard(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        nint hGlobal = 0;
        if (!OpenClipboard(0))
            return false;
        try
        {
            EmptyClipboard();
            var data = Encoding.Unicode.GetBytes(text + "\0");
            hGlobal = GlobalAlloc(Ghnd, (nuint)data.Length);
            if (hGlobal == 0)
                return false;
            var ptr = GlobalLock(hGlobal);
            if (ptr == 0)
            {
                _ = GlobalFree(hGlobal);
                hGlobal = 0;
                return false;
            }

            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
            }
            finally
            {
                _ = GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CfUnicodetext, hGlobal) == 0)
            {
                _ = GlobalFree(hGlobal);
                hGlobal = 0;
                return false;
            }

            hGlobal = 0; // system owns the handle
            return true;
        }
        finally
        {
            CloseClipboard();
            if (hGlobal != 0)
                _ = GlobalFree(hGlobal);
        }
    }
}
