using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Silk.NET.Input;

namespace Cyberland.Demo;

/// <summary>Writes <see cref="Velocity"/> from keybindings / arrows (world units/sec).</summary>
public sealed class DemoInputSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly EntityId _player;
    private readonly SystemScheduler _scheduler;

    public DemoInputSystem(GameHostServices host, EntityId player, SystemScheduler scheduler)
    {
        _host = host;
        _player = player;
        _scheduler = scheduler;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        ref var v = ref world.Components<Velocity>().Get(_player);
        v = default;

        var input = _host.Input;
        if (input is null || input.Keyboards.Count == 0)
            return;

        var kb = input.Keyboards[0];
        var bindings = _host.KeyBindings;

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

        v.X = dx * DemoPlayerConstants.MoveSpeed;
        v.Y = dy * DemoPlayerConstants.MoveSpeed;
    }
}
