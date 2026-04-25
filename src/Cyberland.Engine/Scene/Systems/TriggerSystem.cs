using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Detects trigger overlaps in fixed update and writes per-entity enter/stay/exit events.
/// </summary>
/// <remarks>
/// The system runs pair detection and transition classification in parallel. Event ordering inside each entity buffer is not
/// deterministic, but event membership is deterministic for the same world state.
/// Pairs are skipped when either entity is in the other entity's transform ancestry chain.
/// The cached <see cref="World"/> is used for <see cref="TriggerEvents"/> stores and snapshot merge/commit outside the trigger-only chunk query.
/// <para>
/// With the stock host, this system is registered in the post-mod engine block. Mod fixed-update systems run first,
/// then trigger overlap is computed from those updated transforms. This makes trigger events reflect post-motion poses
/// from the same fixed substep without a separate gameplay-side overlap pass.
/// </para>
/// </remarks>
[RunAfter("cyberland.engine/transform2d")]
public sealed class TriggerSystem : IParallelSystem, IParallelFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Trigger>();

    private readonly List<TriggerSnapshot> _snapshots = new();
    private readonly Dictionary<EntityId, bool> _activeThisTick = new();
    private readonly HashSet<TriggerPairKey> _previousOverlaps = new();
    private readonly HashSet<TriggerPairKey> _currentOverlaps = new();
    private readonly Dictionary<EntityId, List<TriggerEvent>> _mergedEvents = new();
    private readonly List<EntityId> _eventEntities = new();
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
        var world = _world;
        BuildSnapshotsAndEnsureBuffers(world, query);
        if (_snapshots.Count == 0)
        {
            _previousOverlaps.Clear();
            return;
        }

        ClearAllEventBuffers(world);
        BuildCurrentOverlapSet(world, parallelOptions);
        BuildMergedEvents(parallelOptions);
        CommitEventsToBuffers(world);

        _previousOverlaps.Clear();
        foreach (var key in _currentOverlaps)
            _previousOverlaps.Add(key);
    }

    private void BuildSnapshotsAndEnsureBuffers(World world, ChunkQueryAll query)
    {
        _snapshots.Clear();
        _activeThisTick.Clear();

        var transformStore = world.Components<Transform>();

        foreach (var chunk in query)
        {
            var entities = chunk.Entities;
            var triggers = chunk.Column<Trigger>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                if (!transformStore.TryGet(entity, out var transform))
                    continue;

                var trigger = triggers[i];
                _snapshots.Add(new TriggerSnapshot(entity, trigger, in transform));
                _activeThisTick[entity] = true;
            }
        }

        var triggerEventsStore = world.Components<TriggerEvents>();
        for (var i = 0; i < _snapshots.Count; i++)
        {
            var entity = _snapshots[i].Entity;
            ref var triggerEvents = ref triggerEventsStore.GetOrAdd(entity);
            triggerEvents.Events ??= new List<TriggerEvent>(4);
        }
    }

    private void ClearAllEventBuffers(World world)
    {
        var triggerEventsStore = world.Components<TriggerEvents>();
        for (var idx = 0; idx < _snapshots.Count; idx++)
        {
            var entity = _snapshots[idx].Entity;
            ref var ev = ref triggerEventsStore.Get(entity);
            ev.Events?.Clear();
        }
    }

    private void BuildCurrentOverlapSet(World world, ParallelOptions parallelOptions)
    {
        _currentOverlaps.Clear();
        if (_snapshots.Count < 2)
            return;

        var overlapBatches = new ConcurrentBag<List<TriggerPairKey>>();
        var transformStore = world.Components<Transform>();

        Parallel.ForEach(Partitioner.Create(0, _snapshots.Count), parallelOptions, range =>
        {
            var localPairs = new List<TriggerPairKey>(64);
            for (var i = range.Item1; i < range.Item2; i++)
            {
                var a = _snapshots[i];
                if (!a.Trigger.Enabled)
                    continue;

                for (var j = i + 1; j < _snapshots.Count; j++)
                {
                    var b = _snapshots[j];
                    if (!b.Trigger.Enabled)
                        continue;

                    if (SharesTransformHierarchy(world, transformStore, a.Entity, b.Entity))
                        continue;
                    if (!PassesLayerFilter(in a, in b))
                        continue;

                    if (!Overlaps(in a, in b))
                        continue;

                    localPairs.Add(new TriggerPairKey(a.Entity, b.Entity));
                }
            }

            overlapBatches.Add(localPairs);
        });

        foreach (var batch in overlapBatches)
        {
            foreach (var pair in batch)
                _currentOverlaps.Add(pair);
        }
    }

    private void BuildMergedEvents(ParallelOptions parallelOptions)
    {
        _mergedEvents.Clear();
        var snapshotByEntity = new Dictionary<EntityId, TriggerSnapshot>(_snapshots.Count);
        for (var i = 0; i < _snapshots.Count; i++)
            snapshotByEntity[_snapshots[i].Entity] = _snapshots[i];

        var localBuckets = new ConcurrentBag<Dictionary<EntityId, List<TriggerEvent>>>();

        var currentPairs = _currentOverlaps.ToArray();
        Parallel.For(
            0,
            currentPairs.Length,
            parallelOptions,
            () => new Dictionary<EntityId, List<TriggerEvent>>(),
            (i, _, local) =>
            {
                var pair = currentPairs[i];
                var kind = _previousOverlaps.Contains(pair)
                    ? TriggerEventKind.OnTriggerStay
                    : TriggerEventKind.OnTriggerEnter;
                AppendMirrored(local, snapshotByEntity, pair, kind);
                return local;
            },
            local => localBuckets.Add(local));

        var previousPairs = _previousOverlaps.ToArray();
        Parallel.For(
            0,
            previousPairs.Length,
            parallelOptions,
            () => new Dictionary<EntityId, List<TriggerEvent>>(),
            (i, _, local) =>
            {
                var pair = previousPairs[i];
                if (_currentOverlaps.Contains(pair))
                    return local;

                AppendMirrored(local, snapshotByEntity, pair, TriggerEventKind.OnTriggerExit);
                return local;
            },
            local => localBuckets.Add(local));

        foreach (var bucket in localBuckets)
        {
            foreach (var kv in bucket)
            {
                if (!_mergedEvents.TryGetValue(kv.Key, out var list))
                {
                    list = new List<TriggerEvent>(kv.Value.Count);
                    _mergedEvents[kv.Key] = list;
                }

                list.AddRange(kv.Value);
            }
        }
    }

    private void CommitEventsToBuffers(World world)
    {
        var triggerEventsStore = world.Components<TriggerEvents>();
        _eventEntities.Clear();
        foreach (var kv in _mergedEvents)
        {
            if (_activeThisTick.ContainsKey(kv.Key))
                _eventEntities.Add(kv.Key);
        }

        for (var idx = 0; idx < _eventEntities.Count; idx++)
        {
            var entity = _eventEntities[idx];
            var events = _mergedEvents[entity];
            ref var triggerEvents = ref triggerEventsStore.Get(entity);
            triggerEvents.Events!.AddRange(events);
        }
    }

    private static void AppendMirrored(
        Dictionary<EntityId, List<TriggerEvent>> local,
        Dictionary<EntityId, TriggerSnapshot> snapshotByEntity,
        TriggerPairKey pair,
        TriggerEventKind kind)
    {
        var a = pair.A;
        var b = pair.B;
        var aLayer = NormalizeLayer(snapshotByEntity[a].Trigger.LayerMask);
        var bLayer = NormalizeLayer(snapshotByEntity[b].Trigger.LayerMask);
        Append(local, a, new TriggerEvent { Self = a, Other = b, Kind = kind, OtherLayerMask = bLayer });
        Append(local, b, new TriggerEvent { Self = b, Other = a, Kind = kind, OtherLayerMask = aLayer });
    }

    private static void Append(
        Dictionary<EntityId, List<TriggerEvent>> local,
        EntityId entity,
        TriggerEvent value)
    {
        if (!local.TryGetValue(entity, out var list))
        {
            list = new List<TriggerEvent>(4);
            local[entity] = list;
        }

        list.Add(value);
    }

    private static bool SharesTransformHierarchy(
        World world,
        ComponentStore<Transform> transformStore,
        EntityId a,
        EntityId b) =>
        IsInHierarchy(world, transformStore, a, b) || IsInHierarchy(world, transformStore, b, a);

    private static bool IsInHierarchy(
        World world,
        ComponentStore<Transform> transformStore,
        EntityId entity,
        EntityId maybeAncestor)
    {
        if (!transformStore.TryGet(entity, out var transform))
            return false;

        var parent = transform.Parent;
        var guard = 0;
        while (parent.Raw != 0 && world.IsAlive(parent) && guard < 1024)
        {
            if (parent == maybeAncestor)
                return true;

            if (!transformStore.TryGet(parent, out var parentTransform))
                return false;

            parent = parentTransform.Parent;
            guard++;
        }

        return false;
    }

    private static bool PassesLayerFilter(in TriggerSnapshot a, in TriggerSnapshot b)
    {
        var aLayer = NormalizeLayer(a.Trigger.LayerMask);
        var bLayer = NormalizeLayer(b.Trigger.LayerMask);
        var aMask = NormalizeMask(a.Trigger.CollidesWithMask);
        var bMask = NormalizeMask(b.Trigger.CollidesWithMask);
        return (aMask & bLayer) != 0 && (bMask & aLayer) != 0;
    }

    private static uint NormalizeLayer(uint layerMask) => layerMask == 0 ? 1u : layerMask;
    private static uint NormalizeMask(uint collidesWithMask) => collidesWithMask == 0 ? uint.MaxValue : collidesWithMask;

    private static bool Overlaps(in TriggerSnapshot a, in TriggerSnapshot b)
    {
        var aPos = a.Position;
        var bPos = b.Position;
        var aTrigger = a.Trigger;
        var bTrigger = b.Trigger;
        var aRadius = aTrigger.Radius * MathF.Max(MathF.Abs(a.Scale.X), MathF.Abs(a.Scale.Y));
        var bRadius = bTrigger.Radius * MathF.Max(MathF.Abs(b.Scale.X), MathF.Abs(b.Scale.Y));
        var aHalfExtents = new Vector2D<float>(aTrigger.HalfExtents.X * MathF.Abs(a.Scale.X), aTrigger.HalfExtents.Y * MathF.Abs(a.Scale.Y));
        var bHalfExtents = new Vector2D<float>(bTrigger.HalfExtents.X * MathF.Abs(b.Scale.X), bTrigger.HalfExtents.Y * MathF.Abs(b.Scale.Y));
        var aShape = a.Trigger.Shape;
        var bShape = b.Trigger.Shape;

        if (aShape == TriggerShapeKind.Point && bShape == TriggerShapeKind.Point)
            return PointPoint(aPos, bPos);
        if (aShape == TriggerShapeKind.Point && bShape == TriggerShapeKind.Circle)
            return PointCircle(aPos, bPos, bRadius);
        if (aShape == TriggerShapeKind.Circle && bShape == TriggerShapeKind.Point)
            return PointCircle(bPos, aPos, aRadius);
        if (aShape == TriggerShapeKind.Circle && bShape == TriggerShapeKind.Circle)
            return CircleCircle(aPos, aRadius, bPos, bRadius);
        if (aShape == TriggerShapeKind.Point && bShape == TriggerShapeKind.Rectangle)
            return PointOrientedRect(aPos, bPos, b.RotationRadians, bHalfExtents);
        if (aShape == TriggerShapeKind.Rectangle && bShape == TriggerShapeKind.Point)
            return PointOrientedRect(bPos, aPos, a.RotationRadians, aHalfExtents);
        if (aShape == TriggerShapeKind.Circle && bShape == TriggerShapeKind.Rectangle)
            return CircleOrientedRect(aPos, aRadius, bPos, b.RotationRadians, bHalfExtents);
        if (aShape == TriggerShapeKind.Rectangle && bShape == TriggerShapeKind.Circle)
            return CircleOrientedRect(bPos, bRadius, aPos, a.RotationRadians, aHalfExtents);

        return OrientedRectOrientedRect(
            aPos,
            a.RotationRadians,
            aHalfExtents,
            bPos,
            b.RotationRadians,
            bHalfExtents);
    }

    private static bool PointPoint(Vector2D<float> a, Vector2D<float> b)
    {
        const float epsilon = 0.0001f;
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy) <= epsilon * epsilon;
    }

    private static bool PointCircle(Vector2D<float> point, Vector2D<float> center, float radius)
    {
        var r = MathF.Max(radius, 0f);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return (dx * dx) + (dy * dy) <= r * r;
    }

    private static bool CircleCircle(Vector2D<float> aCenter, float aRadius, Vector2D<float> bCenter, float bRadius)
    {
        var ar = MathF.Max(aRadius, 0f);
        var br = MathF.Max(bRadius, 0f);
        var sum = ar + br;
        var dx = aCenter.X - bCenter.X;
        var dy = aCenter.Y - bCenter.Y;
        return (dx * dx) + (dy * dy) <= sum * sum;
    }

    private static bool PointOrientedRect(
        Vector2D<float> point,
        Vector2D<float> rectCenter,
        float rectRotation,
        Vector2D<float> halfExtents)
    {
        var hx = MathF.Abs(halfExtents.X);
        var hy = MathF.Abs(halfExtents.Y);
        RotateIntoLocal(point.X - rectCenter.X, point.Y - rectCenter.Y, rectRotation, out var lx, out var ly);
        return MathF.Abs(lx) <= hx && MathF.Abs(ly) <= hy;
    }

    private static bool CircleOrientedRect(
        Vector2D<float> circleCenter,
        float radius,
        Vector2D<float> rectCenter,
        float rectRotation,
        Vector2D<float> halfExtents)
    {
        var hx = MathF.Abs(halfExtents.X);
        var hy = MathF.Abs(halfExtents.Y);
        var r = MathF.Max(radius, 0f);
        RotateIntoLocal(circleCenter.X - rectCenter.X, circleCenter.Y - rectCenter.Y, rectRotation, out var lx, out var ly);
        var clampedX = Math.Clamp(lx, -hx, hx);
        var clampedY = Math.Clamp(ly, -hy, hy);
        var dx = lx - clampedX;
        var dy = ly - clampedY;
        return (dx * dx) + (dy * dy) <= r * r;
    }

    private static bool OrientedRectOrientedRect(
        Vector2D<float> aCenter,
        float aRotation,
        Vector2D<float> aHalfExtents,
        Vector2D<float> bCenter,
        float bRotation,
        Vector2D<float> bHalfExtents)
    {
        var aHx = MathF.Abs(aHalfExtents.X);
        var aHy = MathF.Abs(aHalfExtents.Y);
        var bHx = MathF.Abs(bHalfExtents.X);
        var bHy = MathF.Abs(bHalfExtents.Y);

        var aAxisX = new Vector2D<float>(MathF.Cos(aRotation), MathF.Sin(aRotation));
        var aAxisY = new Vector2D<float>(-MathF.Sin(aRotation), MathF.Cos(aRotation));
        var bAxisX = new Vector2D<float>(MathF.Cos(bRotation), MathF.Sin(bRotation));
        var bAxisY = new Vector2D<float>(-MathF.Sin(bRotation), MathF.Cos(bRotation));

        var dx = bCenter.X - aCenter.X;
        var dy = bCenter.Y - aCenter.Y;
        var t = new Vector2D<float>(dx, dy);

        Span<Vector2D<float>> axes = stackalloc Vector2D<float>[4];
        axes[0] = aAxisX;
        axes[1] = aAxisY;
        axes[2] = bAxisX;
        axes[3] = bAxisY;

        foreach (ref readonly var axis in axes)
        {
            var tProj = AbsDot(t, axis);
            var aProj = ProjectOrientedRectRadius(axis, aAxisX, aAxisY, aHx, aHy);
            var bProj = ProjectOrientedRectRadius(axis, bAxisX, bAxisY, bHx, bHy);
            if (tProj > aProj + bProj)
                return false;
        }

        return true;
    }

    private static float ProjectOrientedRectRadius(
        in Vector2D<float> axis,
        in Vector2D<float> rectAxisX,
        in Vector2D<float> rectAxisY,
        float hx,
        float hy) =>
        hx * AbsDot(axis, rectAxisX) + hy * AbsDot(axis, rectAxisY);

    private static float AbsDot(in Vector2D<float> a, in Vector2D<float> b) =>
        MathF.Abs((a.X * b.X) + (a.Y * b.Y));

    private static void RotateIntoLocal(float wx, float wy, float worldRotation, out float lx, out float ly)
    {
        var c = MathF.Cos(worldRotation);
        var s = MathF.Sin(worldRotation);
        lx = (wx * c) + (wy * s);
        ly = (-wx * s) + (wy * c);
    }

    private readonly struct TriggerSnapshot
    {
        public TriggerSnapshot(EntityId entity, Trigger trigger, in Transform transform)
        {
            Entity = entity;
            Trigger = trigger;
            // Decompose once per snapshot; snapshots are immutable PRS views used by the overlap math in this file.
            TransformMath.DecomposeToPRS(transform.WorldMatrix, out var pos, out var rad, out var scale);
            Position = pos;
            RotationRadians = rad;
            Parent = transform.Parent;
            Scale = scale;
        }

        public readonly EntityId Entity;
        public readonly Trigger Trigger;
        public readonly Vector2D<float> Position;
        public readonly float RotationRadians;
        public readonly EntityId Parent;
        public readonly Vector2D<float> Scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct TriggerPairKey : IEquatable<TriggerPairKey>
    {
        public TriggerPairKey(EntityId a, EntityId b)
        {
            var aRaw = Math.Min(a.Raw, b.Raw);
            var bRaw = Math.Max(a.Raw, b.Raw);
            A = new EntityId(aRaw);
            B = new EntityId(bRaw);
        }

        public readonly EntityId A;
        public readonly EntityId B;

        public bool Equals(TriggerPairKey other) => A == other.A && B == other.B;
        public override bool Equals(object? obj) => obj is TriggerPairKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
    }
}
