using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Optional camera follow behavior for entities that also have <see cref="Camera2D"/>.
/// </summary>
/// <remarks>
/// Attach this component to a camera entity and set <see cref="Target"/> to the followed entity.
/// <see cref="Systems.CameraFollowSystem"/> updates camera <see cref="Transform.WorldPosition"/> each fixed tick.
/// </remarks>
[RequiresComponent<Camera2D>]
[RequiresComponent<Transform>]
public struct CameraFollow2D : IComponent
{
    /// <summary>
    /// Whether follow is active.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// Target entity to follow.
    /// </summary>
    public EntityId Target;

    /// <summary>
    /// Offset from target position in world units.
    /// </summary>
    public Vector2D<float> OffsetWorld;

    /// <summary>
    /// Follow smoothing in [0..1]; 1 snaps to the target every tick.
    /// </summary>
    public float FollowLerp;

    /// <summary>
    /// Optional world bounds minimum; set together with <see cref="BoundsMaxWorld"/> when clamping is needed.
    /// </summary>
    public Vector2D<float> BoundsMinWorld;

    /// <summary>
    /// Optional world bounds maximum; set together with <see cref="BoundsMinWorld"/> when clamping is needed.
    /// </summary>
    public Vector2D<float> BoundsMaxWorld;

    /// <summary>
    /// Enables bounds clamping when true.
    /// </summary>
    public bool ClampToBounds;
}
