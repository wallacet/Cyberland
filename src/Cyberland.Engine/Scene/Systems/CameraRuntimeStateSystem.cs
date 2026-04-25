using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Publishes a frame-stable active camera snapshot into <see cref="GameHostServices.CameraRuntimeState"/>.
/// This decouples gameplay/layout systems from renderer queue timing.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CameraRuntimeStateSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Camera2D, Transform>();

    /// <summary>Creates the runtime camera state publisher.</summary>
    public CameraRuntimeStateSystem(GameHostServices host) => _host = host;

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
        var fallbackSize = _host.Renderer is null ? new Vector2D<int>(800, 600) : _host.Renderer.SwapchainPixelSize;
        var best = CameraSelection.Default(fallbackSize);
        var found = false;

        foreach (var chunk in query)
        {
            var cameras = chunk.Column<Camera2D>();
            var transforms = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var cam = ref cameras[i];
                if (!cam.Enabled || cam.ViewportSizeWorld.X <= 0 || cam.ViewportSizeWorld.Y <= 0)
                    continue;
                if (found && cam.Priority <= best.Priority)
                    continue;

                ref readonly var tf = ref transforms[i];
                TransformMath.DecomposeToPRS(tf.WorldMatrix, out var worldPos, out var worldRad, out _);
                best = new CameraViewRequest
                {
                    PositionWorld = worldPos,
                    RotationRadians = worldRad,
                    ViewportSizeWorld = cam.ViewportSizeWorld,
                    Priority = cam.Priority,
                    Enabled = true,
                    BackgroundColor = cam.BackgroundColor
                };
                found = true;
            }
        }

        _host.CameraRuntimeState = CameraRuntimeState.FromView(best);
    }
}
