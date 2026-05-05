using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>Publishes mouse-world pointer into <see cref="ControlState"/>.</summary>
public sealed class InputSystem : ISingletonSystem, ISingletonEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ControlState>();

    private readonly GameHostServices _host;

    /// <summary>Creates the input driver (singleton control row; state row resolved at startup).</summary>
    public InputSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity controlRow)
    {
        _ = controlRow;
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
    }
}
