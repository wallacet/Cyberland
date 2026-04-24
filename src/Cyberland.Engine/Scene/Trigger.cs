using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Trigger volume configuration used by <see cref="Systems.TriggerSystem"/> overlap detection.
/// </summary>
/// <remarks>
/// Shapes are evaluated in world space (+Y up). For <see cref="TriggerShapeKind.Rectangle"/>, the trigger uses world
/// center from <see cref="Transform.WorldPosition"/>, orientation from <see cref="Transform.WorldRotationRadians"/>, and local half extents from
/// <see cref="HalfExtents"/>.
/// <para>
/// This component intentionally does not use <see cref="RequiresComponentAttribute{TRequired}"/> for <see cref="Transform"/>:
/// <see cref="Systems.TriggerSystem"/> skips triggers with no transform row so authoring can attach a trigger before a transform exists.
/// </para>
/// </remarks>
public struct Trigger : IComponent
{
    /// <summary>
    /// Whether this trigger participates in overlap checks.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// Shape used for overlap tests.
    /// </summary>
    public TriggerShapeKind Shape;

    /// <summary>
    /// Circle radius in world units when <see cref="Shape"/> is <see cref="TriggerShapeKind.Circle"/>.
    /// </summary>
    public float Radius;

    /// <summary>
    /// Rectangle half extents in world units when <see cref="Shape"/> is <see cref="TriggerShapeKind.Rectangle"/>.
    /// </summary>
    public Vector2D<float> HalfExtents;

    /// <summary>
    /// Default enabled point trigger at the entity's world position.
    /// </summary>
    public static Trigger DefaultPoint
    {
        get
        {
            Trigger t;
            t.Enabled = true;
            t.Shape = TriggerShapeKind.Point;
            t.Radius = 0f;
            t.HalfExtents = default;
            return t;
        }
    }
}
