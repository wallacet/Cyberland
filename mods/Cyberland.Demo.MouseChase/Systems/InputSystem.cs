using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>Publishes mouse-world pointer into <see cref="ControlState"/>.</summary>
public sealed class InputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ControlState>();

    private readonly GameHostServices _host;
    private EntityId _stateEntity;

    /// <summary>Creates the input driver (singleton control row; state row resolved at startup).</summary>
    public InputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity controlRow)
    {
        _stateEntity = controlRow.World.QueryChunks(SystemQuerySpec.All<GameState>())
            .RequireSingleEntityWith<GameState>("mouse chase state");
    }

    /// <inheritdoc />
    public void OnSingletonEarlyUpdate(in SingletonEntity controlRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var input = _host.Input;
        var renderer = _host.Renderer;

        if (input.IsDown("cyberland.common/quit"))
        {
            renderer.RequestClose?.Invoke();
            return;
        }

        ref var control = ref controlRow.Get<ControlState>();

        var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
        control.MouseWorld = new Silk.NET.Maths.Vector2D<float>(mouse.X, mouse.Y);

        ref var game = ref controlRow.World.Get<GameState>(_stateEntity);
        if (game.Phase is RoundPhase.Won or RoundPhase.Lost &&
            input.HasAnyActionPressedThisFrame("cyberland.demo.mousechase/restart", "cyberland.common/start"))
            game.PendingRestartRequest = true;
    }
}
