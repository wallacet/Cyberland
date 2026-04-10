using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Local transform relative to <see cref="Parent"/> (or world if parent is default / invalid).
/// The <see cref="Systems.TransformHierarchySystem"/> writes <see cref="Position"/>, <see cref="Rotation"/>, <see cref="Scale"/> world values each frame.
/// </summary>
public struct Transform
{
    /// <summary>Translation of this node in its parent’s space (or world if root).</summary>
    public Vector2D<float> LocalPosition;
    /// <summary>Local CCW rotation in radians.</summary>
    public float LocalRotationRadians;
    /// <summary>Local non-uniform scale before rotation.</summary>
    public Vector2D<float> LocalScale;

    /// <summary>Parent entity for hierarchy; when <see cref="EntityId.Raw"/> is 0, this node is a root.</summary>
    public EntityId Parent;

    /// <summary>Zero translation/rotation, unit scale, no parent.</summary>
    public static Transform Identity
    {
        get
        {
            Transform t;
            t.LocalPosition = default;
            t.LocalRotationRadians = 0f;
            t.LocalScale = new Vector2D<float>(1f, 1f);
            t.Parent = default;
            return t;
        }
    }
}
