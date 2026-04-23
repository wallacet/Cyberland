using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Local transform relative to <see cref="Parent"/> (or world if parent is default / invalid).
/// </summary>
/// <remarks>
/// <see cref="Systems.TransformHierarchySystem"/> resolves this local hierarchy each frame and writes world-space cache
/// fields (<see cref="WorldPosition"/>, <see cref="WorldRotationRadians"/>, <see cref="WorldScale"/>). Gameplay and render
/// systems should read those world fields instead of keeping separate position/rotation/scale components.
/// </remarks>
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

    /// <summary>Resolved world position in world units (+Y up), written by the hierarchy system.</summary>
    public Vector2D<float> WorldPosition;

    /// <summary>Resolved world rotation in radians, written by the hierarchy system.</summary>
    public float WorldRotationRadians;

    /// <summary>Resolved world non-uniform scale, written by the hierarchy system.</summary>
    public Vector2D<float> WorldScale;

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
            t.WorldPosition = default;
            t.WorldRotationRadians = 0f;
            t.WorldScale = new Vector2D<float>(1f, 1f);
            return t;
        }
    }
}
