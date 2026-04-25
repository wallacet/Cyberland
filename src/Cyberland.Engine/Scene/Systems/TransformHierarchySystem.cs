using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves <see cref="Transform"/> hierarchies by composing each entity's <see cref="Transform.LocalMatrix"/> with its
/// parent chain into <see cref="Transform.WorldMatrix"/>. Forest roots are solved in parallel (disjoint subtrees);
/// cycle leftovers are resolved sequentially.
/// </summary>
/// <remarks>
/// Downstream systems can read the composed <see cref="Transform.WorldMatrix"/> directly or access position/rotation/
/// scale via <see cref="Transform"/>'s PRS properties (decomposed on demand with a per-row cache).
/// </remarks>
public sealed class TransformHierarchySystem : IParallelSystem, IParallelEarlyUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<Transform>();

    // Reuse child adjacency lists across frames to avoid allocating a new List per parent each tick.
    private readonly Stack<List<EntityId>> _childListPool = new();

    private readonly List<EntityId> _order = new();
    private readonly Queue<EntityId> _queue = new();
    private readonly Dictionary<EntityId, List<EntityId>> _children = new();
    private readonly HashSet<EntityId> _tfSet = new();
    private readonly HashSet<EntityId> _visited = new();
    private readonly List<EntityId> _roots = new();
    private readonly Dictionary<EntityId, int> _inDegree = new();

    // Tracks which entities have had WorldMatrix written this frame. Consulted by SolveOne to decide whether a parent's
    // WorldMatrix is fresh or still stale from the previous frame (in which case we fall back to parent.LocalMatrix).
    private readonly ConcurrentDictionary<EntityId, byte> _processed = new();

    // Parallel.For root tasks reuse per-thread BFS scratch to avoid per-root List/Queue allocations.
    private readonly ThreadLocal<List<EntityId>> _bfsOrderTls = new(() => new List<EntityId>(64));
    private readonly ThreadLocal<Queue<EntityId>> _bfsQueueTls = new(() => new Queue<EntityId>(64));

    private World _world = null!;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelEarlyUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        var world = _world;
        var w = _world;

        // Return last frame's adjacency lists to the pool before rebuilding (avoids per-parent List alloc when stable).
        foreach (var kv in _children)
        {
            kv.Value.Clear();
            _childListPool.Push(kv.Value);
        }

        _children.Clear();
        _tfSet.Clear();
        _order.Clear();
        _queue.Clear();
        _visited.Clear();
        _roots.Clear();
        _inDegree.Clear();
        _processed.Clear();

        foreach (var view in query)
        {
            var ents = view.Entities;
            for (var i = 0; i < view.Count; i++)
                _tfSet.Add(ents[i]);
        }

        foreach (var id in _tfSet)
            _inDegree[id] = 0;

        foreach (var id in _tfSet)
        {
            ref readonly var t = ref w.Get<Transform>(id);
            var p = t.Parent;
            if (p.Raw == 0 || !world.IsAlive(p) || !_tfSet.Contains(p))
                continue;

            if (!_children.TryGetValue(p, out var list))
            {
                list = _childListPool.Count > 0 ? _childListPool.Pop() : new List<EntityId>(8);
                _children[p] = list;
            }

            list.Add(id);
            _inDegree[id]++;
        }

        foreach (var id in _tfSet)
        {
            if (_inDegree[id] == 0)
            {
                _queue.Enqueue(id);
                _roots.Add(id);
            }
        }

        while (_queue.Count > 0)
        {
            var id = _queue.Dequeue();
            _order.Add(id);
            _visited.Add(id);
            if (!_children.TryGetValue(id, out var ch))
                continue;
            foreach (var c in ch)
            {
                _inDegree[c]--;
                if (_inDegree[c] == 0)
                    _queue.Enqueue(c);
            }
        }

        foreach (var id in _tfSet)
        {
            if (!_visited.Contains(id))
                _order.Add(id);
        }

        if (_roots.Count > 0)
        {
            Parallel.For(0, _roots.Count, parallelOptions, i =>
            {
                var root = _roots[i];
                var subOrder = BuildSubtreeBfsOrder(root);
                // BFS order guarantees parents are solved before children inside the subtree,
                // so SolveOne can read the parent's fresh WorldMatrix directly from the store.
                foreach (var eid in subOrder)
                    SolveOne(eid);
            });
        }

        // Cycle leftovers: anything in _order that parallel BFS didn't reach (topologically impossible in a cycle).
        // We fall back to parent.LocalMatrix when the parent hasn't been processed yet — this matches the prior
        // "eventually completes" semantics without ever stalling the frame on unresolved cycles.
        foreach (var id in _order)
        {
            if (_processed.ContainsKey(id))
                continue;

            SolveOne(id);
        }
    }

    private List<EntityId> BuildSubtreeBfsOrder(EntityId root)
    {
        var order = _bfsOrderTls.Value!;
        var q = _bfsQueueTls.Value!;
        order.Clear();
        q.Clear();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var id = q.Dequeue();
            order.Add(id);
            if (_children.TryGetValue(id, out var ch))
            {
                foreach (var c in ch)
                    q.Enqueue(c);
            }
        }

        return order;
    }

    private void SolveOne(EntityId id)
    {
        var world = _world;
        var w = _world;
        ref var t = ref w.Get<Transform>(id);
        var parentM = Matrix3x2.Identity;
        var p = t.Parent;
        if (p.Raw != 0 && world.IsAlive(p) && w.Has<Transform>(p))
        {
            ref readonly var parentT = ref w.Get<Transform>(p);
            // If the parent was already solved this frame, its WorldMatrix is fresh (tree order guarantees this within
            // a subtree). Otherwise (cycle fallback) use the parent's local matrix so cycles still converge across
            // frames without reading last-frame world state.
            parentM = _processed.ContainsKey(p) ? parentT.WorldMatrix : parentT.LocalMatrix;
        }

        t.WorldMatrix = TransformMath.Compose(parentM, t.LocalMatrix);
        _processed.TryAdd(id, 0);
    }
}
