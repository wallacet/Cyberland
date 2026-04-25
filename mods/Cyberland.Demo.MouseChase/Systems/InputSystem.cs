using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class InputSystem : ISystem, IEarlyUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;

    private readonly GameHostServices _host;
    private readonly EntityId _controlEntity;
    private readonly EntityId _stateEntity;
    private World _world = null!;

    public InputSystem(GameHostServices host, EntityId controlEntity, EntityId stateEntity)
    {
        _host = host;
        _controlEntity = controlEntity;
        _stateEntity = stateEntity;
    }

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    public void OnEarlyUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = query;
        _ = deltaSeconds;

        var input = _host.Input;
        var renderer = _host.Renderer;
        if (input is null || renderer is null)
            return;

        if (input.IsDown("cyberland.common/quit"))
        {
            renderer.RequestClose?.Invoke();
            return;
        }

        ref var control = ref _world.Components<ControlState>().Get(_controlEntity);
        ref readonly var state = ref _world.Components<GameState>().Get(_stateEntity);

        var mouse = input.GetMousePosition(CoordinateSpace.WorldSpace);
        control.MouseWorld = new Silk.NET.Maths.Vector2D<float>(mouse.X, mouse.Y);
        control.ZoomDelta = input.ReadAxis("cyberland.demo.mousechase/zoom");
        control.PrimaryPressed = input.WasPressed("cyberland.demo.mousechase/primary");
        control.RestartPressed = input.WasPressed("cyberland.demo.mousechase/restart");

        if (state.Phase is RoundPhase.Won or RoundPhase.Lost && input.WasPressed("cyberland.common/start"))
            control.RestartPressed = true;
    }
}
