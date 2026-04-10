using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D;

/// <summary>
/// Local transform relative to <see cref="Parent"/> (or world if parent is default / invalid).
/// The <see cref="TransformHierarchySystem"/> writes <see cref="Position"/>, <see cref="Rotation"/>, <see cref="Scale"/> world values each frame.
/// </summary>
public struct Transform
{
    public Vector2D<float> LocalPosition;
    public float LocalRotationRadians;
    public Vector2D<float> LocalScale;

    /// <summary>When <see cref="EntityId.Raw"/> is 0, this node is a hierarchy root.</summary>
    public EntityId Parent;

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
