using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo;

/// <summary>
/// Polls keyboard (sequential early update): writes <see cref="Velocity"/> from keybindings and edge arrow taps, toggles optional
/// velocity damping (F9), and forwards <c>Q</c> to <see cref="Rendering.IRenderer.RequestClose"/> when set by the host.
/// </summary>
public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Velocity>();

    private readonly GameHostServices _host;
    private readonly SystemScheduler _scheduler;
    private bool _initialized;

    public InputSystem(GameHostServices host, SystemScheduler scheduler)
    {
        _host = host;
        _scheduler = scheduler;
    }

    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
        _ = _host.Input
            ?? throw new InvalidOperationException("cyberland.demo/input requires Host.Input during OnStart.");

        _ = archetype.RequireSingleEntityWith<PlayerTag>("player");
        _initialized = true;
    }

    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;
        if (!_initialized)
            return;

        var input = _host.Input!;

        foreach (var chunk in archetype)
        {
            var vels = chunk.Column<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
                vels[i] = default;
        }

        if (input.IsDown("cyberland.common/quit"))
        {
            _host.Renderer?.RequestClose?.Invoke();
            return;
        }

        if (input.WasPressed("cyberland.demo/toggle_velocity_damp"))
        {
            var id = "cyberland.demo/velocity-damp";
            _scheduler.SetEnabled(id, !_scheduler.IsEnabled(id));
        }

        float dx = input.ReadAxis("cyberland.demo/move_x");
        float dy = input.ReadAxis("cyberland.demo/move_y");

        if (dx == 0f && dy == 0f)
            return;

        var len = MathF.Sqrt(dx * dx + dy * dy);
        dx /= len;
        dy /= len;

        foreach (var chunk in archetype)
        {
            var vels = chunk.Column<Velocity>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var v = ref vels[i];
                v.X = dx * Constants.MoveSpeed;
                v.Y = dy * Constants.MoveSpeed;
            }
        }
    }
}
