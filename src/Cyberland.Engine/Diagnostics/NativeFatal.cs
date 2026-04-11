using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Diagnostics;

/// <summary>Native fatal path: one dialog then process exit.</summary>
[ExcludeFromCodeCoverage(Justification = "Shows MessageBox and calls Environment.Exit.")]
internal static class NativeFatal
{
    internal static void Deliver(string title, string message)
    {
        UserMessageDialog.ShowError(title, message);
        Environment.Exit(1);
    }
}
