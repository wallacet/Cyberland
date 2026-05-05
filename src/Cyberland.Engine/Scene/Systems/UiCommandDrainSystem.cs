using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Serial late pass that drains <see cref="GameHostServices.UiCommands"/> immediately after UI dispatch when a dispatcher is installed.
/// </summary>
public sealed class UiCommandDrainSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

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
        while (_host.UiCommands.TryDequeue(out var cmd))
            dispatch?.Invoke(cmd);
    }
}
