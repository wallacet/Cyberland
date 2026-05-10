using System.Collections.Concurrent;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;

namespace Cyberland.Demo;

/// <summary>
/// Early update: clears <see cref="Velocity"/> in parallel, reads axes once (same thread as the scheduler early phase), then
/// writes velocities in parallel. Matches host <see cref="ParallelOptions"/> for row partitioning.
/// </summary>
/// <remarks>
/// <see cref="IParallelEarlyUpdate"/> still runs on the thread that executes <see cref="Tasks.SystemScheduler.RunFrame"/>; inner
/// <see cref="Parallel.ForEach"/> joins before axis reads so input polling stays ordered after the clear barrier.
/// </remarks>
public sealed class InputSystem : IParallelSystem, IParallelEarlyUpdate
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

        _ = archetype.RequireSingleEntityWith<PlayerTag>("player");
    }

    /// <inheritdoc />
    public void OnParallelEarlyUpdate(ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;

        foreach (var chunk in archetype)
        {
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var vels = chunk.Column<Velocity>();
                for (var i = range.Item1; i < range.Item2; i++)
                    vels[i] = default;
            });
        }

        var input = _host.Input!;

        if (input.IsDown("cyberland.common/quit"))
        {
            _host.Renderer.RequestClose?.Invoke();
            return;
        }

        if (input.HasActionPressedThisFrame("cyberland.demo/toggle_velocity_damp"))
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
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var vels = chunk.Column<Velocity>();
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    ref var v = ref vels[i];
                    v.X = dx * Constants.MoveSpeed;
                    v.Y = dy * Constants.MoveSpeed;
                }
            });
        }
    }
}
