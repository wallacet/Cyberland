using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Serial late pass: submits one <see cref="CameraViewRequest"/> per eligible <see cref="Camera2D"/> row from the
/// owning <see cref="Transform"/>'s world pose.
/// </summary>
/// <remarks>
/// The stock renderer resolves equal-priority cameras with a first-wins tie-break, so this system stays serial to
/// preserve deterministic submit order aligned with chunk iteration.
/// </remarks>
public sealed class CameraSubmitSystem : ISystem, ILateUpdate
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
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        foreach (var chunk in query)
        {
            var cameras = chunk.Column<Camera2D>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var cam = ref cameras[i];
                if (!cam.Enabled || cam.ViewportSizeWorld.X <= 0 || cam.ViewportSizeWorld.Y <= 0)
                    continue;

                ref readonly var tf = ref transforms[i];
                // Single decomposition feeds position + rotation — property access on a readonly ref would
                // decompose twice because of the defensive copy.
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out var worldRad, out _);
                var request = new CameraViewRequest
                {
                    PositionWorld = worldPos,
                    RotationRadians = worldRad,
                    ViewportSizeWorld = cam.ViewportSizeWorld,
                    PresentationViewportSizeWorld = cam.PresentationViewportSizeWorld,
                    Priority = cam.Priority,
                    Enabled = true,
                    BackgroundColor = cam.BackgroundColor
                };
                r.SubmitCamera(in request);
            }
        }
    }
}
