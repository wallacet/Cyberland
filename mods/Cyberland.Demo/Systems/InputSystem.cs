using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo;

/// <summary>
/// Polls keyboard (sequential early update): writes <see cref="Velocity"/> from keybindings and edge arrow taps, toggles optional
/// velocity damping (F9), and forwards <c>Q</c> to <see cref="Rendering.IRenderer.RequestClose"/> when set by the host.
/// </summary>
public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private readonly SystemScheduler _scheduler;
    private EntityId _player;
    private bool _initialized;

    public InputSystem(GameHostServices host, SystemScheduler scheduler)
    {
        _host = host;
        _scheduler = scheduler;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = archetype;
        var input = _host.Input
                    ?? throw new InvalidOperationException("cyberland.demo/input requires Host.Input during OnStart.");
        if (input.Keyboards.Count == 0)
            throw new InvalidOperationException("cyberland.demo/input requires at least one keyboard during OnStart.");

        _player = archetype.RequireSingleEntityWith<PlayerTag>("player");
        _initialized = true;
    }

    public void OnEarlyUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = archetype;
        _ = deltaSeconds;
        if (!_initialized)
            return;

        ref var v = ref world.Components<Velocity>().Get(_player);
        v = default;

        var kb = _host.Input!.Keyboards[0];
        var bindings = _host.KeyBindings;

        if (kb.IsKeyPressed(Key.Q))
        {
            _host.Renderer?.RequestClose?.Invoke();
            return;
        }

        if (kb.IsKeyPressed(Key.F9) && _scheduler.TryGetEnabled("cyberland.demo/velocity-damp", out var dampOn))
            _scheduler.SetEnabled("cyberland.demo/velocity-damp", !dampOn);

        float dx = 0f, dy = 0f;
        if (bindings.IsDown(kb, "move_left") || kb.IsKeyPressed(Key.Left))
            dx -= 1f;
        if (bindings.IsDown(kb, "move_right") || kb.IsKeyPressed(Key.Right))
            dx += 1f;
        if (bindings.IsDown(kb, "move_up") || kb.IsKeyPressed(Key.Up))
            dy += 1f;
        if (bindings.IsDown(kb, "move_down") || kb.IsKeyPressed(Key.Down))
            dy -= 1f;

        if (dx == 0f && dy == 0f)
            return;

        var len = MathF.Sqrt(dx * dx + dy * dy);
        dx /= len;
        dy /= len;

        v.X = dx * Constants.MoveSpeed;
        v.Y = dy * Constants.MoveSpeed;
    }
}
