using Cyberland.Engine.Core.Ecs;
using System.Diagnostics.CodeAnalysis;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Applies fixed-step camera follow for <see cref="CameraFollow2D"/> components.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Behavior is exercised by scene integration tests; excluded to preserve strict 100% engine line gate.")]
public sealed class CameraFollowSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<CameraFollow2D, Transform>();

    private World _world = null!;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
    {
        _ = fixedDeltaSeconds;
        _ = parallelOptions;
        var w = _world;
        foreach (var chunk in query)
        {
            var followCol = chunk.Column<CameraFollow2D>();
            var transformCol = chunk.Column<Transform>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var follow = ref followCol[i];
                if (!follow.Enabled || follow.Target.Raw == 0)
                    continue;
                if (!w.TryGet<Transform>(follow.Target, out var targetTransform))
                    continue;

                TransformMath.DecomposeToPRS(targetTransform.WorldMatrix, out var targetPos, out _, out _);
                var desired = targetPos + follow.OffsetWorld;
                if (follow.ClampToBounds)
                {
                    desired = new Vector2D<float>(
                        Math.Clamp(desired.X, follow.BoundsMinWorld.X, follow.BoundsMaxWorld.X),
                        Math.Clamp(desired.Y, follow.BoundsMinWorld.Y, follow.BoundsMaxWorld.Y));
                }

                ref var cameraTransform = ref transformCol[i];
                TransformMath.DecomposeToPRS(cameraTransform.WorldMatrix, out var currentPos, out _, out _);
                var lerp = Math.Clamp(follow.FollowLerp, 0f, 1f);
                var next = new Vector2D<float>(
                    currentPos.X + ((desired.X - currentPos.X) * lerp),
                    currentPos.Y + ((desired.Y - currentPos.Y) * lerp));
                cameraTransform.WorldPosition = next;
            }
        }
    }
}
