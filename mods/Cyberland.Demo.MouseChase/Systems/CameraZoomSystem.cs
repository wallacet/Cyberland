using Cyberland.Demo.MouseChase.Components;
using System.Collections.Concurrent;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class CameraZoomSystem : IParallelSystem, IParallelFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Camera2D>();

    private World _world = null!;
    private EntityId _controlEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
        _controlEntity = world.QueryChunks(SystemQuerySpec.All<ControlState>())
            .RequireSingleEntityWith<ControlState>("control state");
    }

    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        ref readonly var control = ref _world.Get<ControlState>(_controlEntity);
        if (MathF.Abs(control.ZoomDelta) <= 0.001f)
            return;

        var step = control.ZoomDelta > 0f ? 0.93f : 1.07f;
        foreach (var chunk in query)
        {
            Parallel.ForEach(Partitioner.Create(0, chunk.Count), parallelOptions, range =>
            {
                var cameras = chunk.Column<Camera2D>();
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    ref var camera = ref cameras[i];
                    var width = Math.Clamp((int)(camera.ViewportSizeWorld.X * step), 880, 1600);
                    var height = Math.Clamp((int)(camera.ViewportSizeWorld.Y * step), 500, 900);
                    camera.ViewportSizeWorld = new Vector2D<int>(width, height);
                }
            });
        }
    }
}
