using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Cyberland.Engine.Rendering;

/// <summary>Shows a blocking error for graphics failures; uses a native message box on Windows, otherwise stderr.</summary>
public static class UserMessageDialog
{
    private const uint MbOk = 0;
    private const uint MbIconError = 0x00000010;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint MessageBoxW(nint hWnd, string? text, string? caption, uint type);

    /// <summary>Native message box path (not unit-tested). Falls back to <see cref="WriteErrorToStderr"/> on failure.</summary>
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

    internal static void WriteErrorToStderr(string title, string message)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== " + title + " ===");
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
    }
}
