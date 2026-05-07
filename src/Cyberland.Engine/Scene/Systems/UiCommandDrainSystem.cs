using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Diagnostics;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Serial late pass that dispatches queued UI commands immediately after <see cref="UiDocumentFrameSystem"/>.
/// </summary>
/// <remarks>
/// Commands remain queued while <see cref="GameHostServices.UiCommandDispatcher"/> is <see langword="null"/> up to a
/// fixed cap; overflow drops oldest commands to avoid unbounded memory growth when command handling is never attached.
/// </remarks>
public sealed class UiCommandDrainSystem : ISystem, ILateUpdate
{
    private const int MaxBufferedCommandsWithoutDispatcher = 4096;
    private const int MaxCommandsPerFrame = 512;
    private readonly GameHostServices _host;
    private bool _reportedMissingDispatcherBackpressure;
    private bool _reportedPerFrameBudgetBackpressure;

    /// <summary>Creates the drain helper.</summary>
    public UiCommandDrainSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = query;
        _ = deltaSeconds;

        var dispatch = _host.UiCommandDispatcher;
        if (dispatch is null)
        {
            var dropped = _host.UiCommands.TrimToMaxCount(MaxBufferedCommandsWithoutDispatcher);
            if (dropped > 0 && !_reportedMissingDispatcherBackpressure)
            {
                EngineDiagnostics.Report(
                    EngineErrorSeverity.Warning,
                    "Cyberland.Engine.UiCommandDrainSystem",
                    $"UiCommandDispatcher was null while UI commands queued; dropped {dropped} oldest commands to cap backlog at {MaxBufferedCommandsWithoutDispatcher}. Attach GameHostServices.UiCommandDispatcher before gameplay UI can enqueue sustained command traffic.");
                _reportedMissingDispatcherBackpressure = true;
            }
            return;
        }

        var drained = 0;
        while (drained < MaxCommandsPerFrame && _host.UiCommands.TryDequeue(out var cmd))
        {
            if (cmd is null)
                continue;
            dispatch.Invoke(cmd);
            drained++;
        }

        if (_host.UiCommands.Count > 0 && !_reportedPerFrameBudgetBackpressure)
        {
            EngineDiagnostics.Report(
                EngineErrorSeverity.Warning,
                "Cyberland.Engine.UiCommandDrainSystem",
                $"UI command drain hit per-frame budget ({MaxCommandsPerFrame}); { _host.UiCommands.Count } commands remain queued for next frame.");
            _reportedPerFrameBudgetBackpressure = true;
        }
    }
}
