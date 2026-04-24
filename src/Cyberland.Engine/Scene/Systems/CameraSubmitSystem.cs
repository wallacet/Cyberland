using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel late pass: submits one <see cref="CameraViewRequest"/> per active <see cref="Camera2D"/> row from the
/// owning <see cref="Transform"/>'s world pose. The renderer picks the highest-priority enabled entry each frame.
/// </summary>
public sealed class CameraSubmitSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Camera2D, Transform>();

    /// <summary>Creates the system.</summary>
    public CameraSubmitSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref readonly var cam = ref chunk.Column<Camera2D>()[i];
                if (!cam.Enabled)
                    return;

                ref readonly var tf = ref chunk.Column<Transform>()[i];
                // Single decomposition feeds position + rotation — property access on a readonly ref would
                // decompose twice because of the defensive copy.
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out var worldRad, out _);
                var request = new CameraViewRequest
                {
                    PositionWorld = worldPos,
                    RotationRadians = worldRad,
                    ViewportSizeWorld = cam.ViewportSizeWorld,
                    Priority = cam.Priority,
                    Enabled = true,
                    BackgroundColor = cam.BackgroundColor
                };
                r.SubmitCamera(in request);
            });
        }
    }
}
