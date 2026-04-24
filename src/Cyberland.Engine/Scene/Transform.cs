using System.Numerics;
using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene;

/// <summary>
/// 2D transform relative to <see cref="Parent"/> (or world if parent is default / invalid).
/// Canonical storage is the <see cref="LocalMatrix"/> / <see cref="WorldMatrix"/> homogeneous 2D affine pair;
/// position / rotation / scale are exposed as properties that decompose the matrix on demand and rebuild it on assignment.
/// </summary>
/// <remarks>
/// <para>
/// The underlying <see cref="Matrix3x2"/> is the standard .NET homogeneous 2D transform: the implicit third column
/// <c>(0, 0, 1)</c> makes it a 3×3 homogeneous affine matrix in six floats.
/// </para>
/// <para>
/// <see cref="Systems.TransformHierarchySystem"/> composes <see cref="WorldMatrix"/> each early-update from parent
/// chains; gameplay and render systems should read either <see cref="WorldMatrix"/> directly (cheapest; no
/// decomposition) or the <c>World*</c> PRS properties (decomposes once and caches until the matrix changes again).
/// </para>
/// <para>
/// <b>Thread safety:</b> the PRS property getters lazily write a cache back into the struct. Call them on a mutable
/// reference (e.g. <c>ref var t = ref store.Get(id)</c>) so the cache persists, or use the <c>ref readonly</c> /
/// value-copy form when the caller tolerates repeated decomposition. Concurrent access from multiple threads to the
/// same row is not supported — the parallel hierarchy pass keeps subtrees disjoint for that reason.
/// </para>
/// <para>
/// <b>Authoring convention:</b> always seed a new transform from <see cref="Identity"/> before assigning PRS
/// properties — a default-initialized struct has a zero matrix, which would decompose to zero scale and make
/// subsequent property setters collapse the transform.
/// </para>
/// </remarks>
public struct Transform : IComponent
{
    /// <summary>Local-to-parent 2D affine transform (scale → rotation → translation).</summary>
    public Matrix3x2 LocalMatrix;

    /// <summary>Resolved world-space 2D affine transform, written by <see cref="Systems.TransformHierarchySystem"/>.</summary>
    public Matrix3x2 WorldMatrix;

    /// <summary>Parent entity for hierarchy; when <see cref="EntityId.Raw"/> is 0, this node is a root.</summary>
    public EntityId Parent;

    // Lazy decomposition cache. Each side (local / world) keeps the last matrix it decomposed and the resulting PRS,
    // so repeated reads on a mutable ref only decompose when the matrix actually changes. On ref-readonly access the
    // cache still works per call (the decompose-on-miss path runs), but cache writes are lost with the defensive copy.
    private Matrix3x2 _localCacheKey;
    private Vector2D<float> _localPos;
    private float _localRad;
    private Vector2D<float> _localScale;
    private bool _localCacheValid;

    private Matrix3x2 _worldCacheKey;
    private Vector2D<float> _worldPos;
    private float _worldRad;
    private Vector2D<float> _worldScale;
    private bool _worldCacheValid;

    /// <summary>Translation of this node in its parent's space (or world if root).</summary>
    public Vector2D<float> LocalPosition
    {
        get { RefreshLocalPrs(); return _localPos; }
        set
        {
            RefreshLocalPrs();
            _localPos = value;
            LocalMatrix = TransformMath.MatrixFromPositionRotationScale(_localPos, _localRad, _localScale);
            _localCacheKey = LocalMatrix;
            _localCacheValid = true;
        }
    }

    /// <summary>Local CCW rotation in radians.</summary>
    public float LocalRotationRadians
    {
        get { RefreshLocalPrs(); return _localRad; }
        set
        {
            RefreshLocalPrs();
            _localRad = value;
            LocalMatrix = TransformMath.MatrixFromPositionRotationScale(_localPos, _localRad, _localScale);
            _localCacheKey = LocalMatrix;
            _localCacheValid = true;
        }
    }

    /// <summary>Local non-uniform scale before rotation.</summary>
    public Vector2D<float> LocalScale
    {
        get { RefreshLocalPrs(); return _localScale; }
        set
        {
            RefreshLocalPrs();
            _localScale = value;
            LocalMatrix = TransformMath.MatrixFromPositionRotationScale(_localPos, _localRad, _localScale);
            _localCacheKey = LocalMatrix;
            _localCacheValid = true;
        }
    }

    /// <summary>Resolved world position in world units (+Y up), derived from <see cref="WorldMatrix"/>.</summary>
    public Vector2D<float> WorldPosition
    {
        get { RefreshWorldPrs(); return _worldPos; }
        set
        {
            RefreshWorldPrs();
            _worldPos = value;
            WorldMatrix = TransformMath.MatrixFromPositionRotationScale(_worldPos, _worldRad, _worldScale);
            _worldCacheKey = WorldMatrix;
            _worldCacheValid = true;
        }
    }

    /// <summary>Resolved world rotation in radians, derived from <see cref="WorldMatrix"/>.</summary>
    public float WorldRotationRadians
    {
        get { RefreshWorldPrs(); return _worldRad; }
        set
        {
            RefreshWorldPrs();
            _worldRad = value;
            WorldMatrix = TransformMath.MatrixFromPositionRotationScale(_worldPos, _worldRad, _worldScale);
            _worldCacheKey = WorldMatrix;
            _worldCacheValid = true;
        }
    }

    /// <summary>Resolved world non-uniform scale, derived from <see cref="WorldMatrix"/>.</summary>
    public Vector2D<float> WorldScale
    {
        get { RefreshWorldPrs(); return _worldScale; }
        set
        {
            RefreshWorldPrs();
            _worldScale = value;
            WorldMatrix = TransformMath.MatrixFromPositionRotationScale(_worldPos, _worldRad, _worldScale);
            _worldCacheKey = WorldMatrix;
            _worldCacheValid = true;
        }
    }

    /// <summary>Identity transform: identity local/world matrices, no parent.</summary>
    public static Transform Identity
    {
        get
        {
            Transform t;
            t.LocalMatrix = Matrix3x2.Identity;
            t.WorldMatrix = Matrix3x2.Identity;
            t.Parent = default;
            t._localCacheKey = Matrix3x2.Identity;
            t._localPos = default;
            t._localRad = 0f;
            t._localScale = new Vector2D<float>(1f, 1f);
            t._localCacheValid = true;
            t._worldCacheKey = Matrix3x2.Identity;
            t._worldPos = default;
            t._worldRad = 0f;
            t._worldScale = new Vector2D<float>(1f, 1f);
            t._worldCacheValid = true;
            return t;
        }
    }

    // Decomposes LocalMatrix into cached PRS fields if the cached key no longer matches the stored matrix.
    // Kept inline to avoid a call overhead on property hot paths.
    private void RefreshLocalPrs()
    {
        if (_localCacheValid && _localCacheKey.Equals(LocalMatrix))
            return;

        TransformMath.DecomposeToPRS(LocalMatrix, out _localPos, out _localRad, out _localScale);
        _localCacheKey = LocalMatrix;
        _localCacheValid = true;
    }

    private void RefreshWorldPrs()
    {
        if (_worldCacheValid && _worldCacheKey.Equals(WorldMatrix))
            return;

        TransformMath.DecomposeToPRS(WorldMatrix, out _worldPos, out _worldRad, out _worldScale);
        _worldCacheKey = WorldMatrix;
        _worldCacheValid = true;
    }
}
