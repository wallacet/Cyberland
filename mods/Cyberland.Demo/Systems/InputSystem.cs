using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo;

/// <summary>
/// Early (sequential) input: maps bound axes to the player’s <see cref="Velocity"/> and exposes a dev toggle for the parallel
/// damp system. Runs before fixed update so integration always sees the latest intent.
/// </summary>
/// <remarks>
/// Pattern: zero velocity for every row, then set from axes if any. That makes “no keys” unambiguous and keeps multi-entity
/// queries safe even though this demo only spawns one player.
/// </remarks>
public sealed class InputSystem : ISystem, IEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, Velocity>();

    private readonly GameHostServices _host;
    private readonly SystemScheduler _scheduler;

    /// <summary>Needs <paramref name="scheduler"/> so F9 can enable/disable <c>cyberland.demo/velocity-damp</c> at runtime.</summary>
    public InputSystem(GameHostServices host, SystemScheduler scheduler)
    {
        _host = host;
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll archetype)
    {
        _ = world;
        _ = archetype;
        _ = _host.Input
            ?? throw new InvalidOperationException("cyberland.demo/input requires Host.Input during OnStart.");

        _ = archetype.RequireSingleEntityWith<PlayerTag>("player");
    }

    /// <inheritdoc />
    public void OnEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds)
    {
        _ = deltaSeconds;

        var input = _host.Input!;

        // Idle unless movement keys are held: avoids drifting velocity when axes settle near zero.
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

        // Diagonal movement matches cardinal speed: normalize before scaling by Constants.MoveSpeed.
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
