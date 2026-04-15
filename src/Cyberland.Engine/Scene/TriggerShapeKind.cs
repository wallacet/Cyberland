namespace Cyberland.Engine.Scene;

/// <summary>
/// Trigger shape used by <see cref="Trigger"/> for overlap tests.
/// </summary>
public enum TriggerShapeKind
{
    /// <summary>
    /// A single world-space point at the entity <see cref="Position"/>.
    /// </summary>
    Point = 0,

    /// <summary>
    /// A world-space circle centered at <see cref="Position"/> with radius from <see cref="Trigger.Radius"/>.
    /// </summary>
    Circle = 1,

    /// <summary>
    /// An oriented world-space rectangle centered at <see cref="Position"/>, rotated by <see cref="Rotation.Radians"/>,
    /// with half extents from <see cref="Trigger.HalfExtents"/>.
    /// </summary>
    Rectangle = 2,
}
