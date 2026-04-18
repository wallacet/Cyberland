namespace Cyberland.Engine.Tests;

/// <summary>Serializes tests that assign <see cref="Cyberland.Engine.Diagnostics.EngineDiagnostics.SinkOverride"/> so parallel runs cannot cross-deliver diagnostics.</summary>
[CollectionDefinition("EngineDiagnostics")]
public sealed class EngineDiagnosticsTestCollection
{
}
