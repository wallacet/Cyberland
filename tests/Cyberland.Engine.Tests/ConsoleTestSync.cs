namespace Cyberland.Engine.Tests;

/// <summary>
/// Serializes tests that call <see cref="Console.SetError"/> — the handle is process-global; parallel runs can
/// otherwise restore <see cref="Console.Error"/> to another test’s disposed <see cref="System.IO.StringWriter"/>.
/// </summary>
internal static class ConsoleTestSync
{
    internal static readonly object ErrorRedirectLock = new();
}
