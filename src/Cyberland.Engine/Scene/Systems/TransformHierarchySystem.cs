using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves <see cref="Transform"/> hierarchies into world transform cache fields.
/// Forest roots are solved in parallel (disjoint subtrees); cycle leftovers are resolved sequentially.
/// </summary>
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
    private readonly Dictionary<EntityId, Matrix3x2> _world = new();
    private readonly List<EntityId> _roots = new();
    private readonly Dictionary<EntityId, int> _inDegree = new();
    private readonly ConcurrentDictionary<EntityId, byte> _processed = new();

    // Parallel.ForEach root tasks reuse per-thread scratch to avoid per-root List/Dictionary allocations.
    private readonly ThreadLocal<List<EntityId>> _bfsOrderTls = new(() => new List<EntityId>(64));
    private readonly ThreadLocal<Queue<EntityId>> _bfsQueueTls = new(() => new Queue<EntityId>(64));
    private readonly ThreadLocal<Dictionary<EntityId, Matrix3x2>> _localWorldTls =
        new(() => new Dictionary<EntityId, Matrix3x2>(64));

    /// <inheritdoc />
    public void OnParallelEarlyUpdate(World world, ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
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
        _world.Clear();
        _roots.Clear();
        _inDegree.Clear();
        _processed.Clear();

        var tfStore = world.Components<Transform>();
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
            ref readonly var t = ref tfStore.Get(id);
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
                var localW = _localWorldTls.Value!;
                localW.Clear();
                var subOrder = BuildSubtreeBfsOrder(root);
                foreach (var eid in subOrder)
                    SolveOne(world, tfStore, eid, localW, _processed);
            });
        }

        foreach (var id in _order)
        {
            if (_processed.ContainsKey(id))
                continue;

            // Same parent resolution as parallel SolveOne, using the shared world matrix map built for this frame.
            SolveOne(world, tfStore, id, _world, _processed);
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

    private void SolveOne(
        World world,
        ComponentStore<Transform> tfStore,
        EntityId id,
        Dictionary<EntityId, Matrix3x2> localW,
        ConcurrentDictionary<EntityId, byte> processed)
    {
        ref readonly var t = ref tfStore.Get(id);
        var local = TransformMath.LocalMatrix(in t);
        Matrix3x2 parentM;
        var p = t.Parent;
        if (p.Raw == 0 || !world.IsAlive(p))
            parentM = Matrix3x2.Identity;
        else if (localW.TryGetValue(p, out var pm))
            parentM = pm;
        else
            parentM = TransformMath.LocalMatrix(in tfStore.Get(p));

        var wM = TransformMath.Compose(parentM, local);
        localW[id] = wM;
        TransformMath.DecomposeToPRS(wM, out var pos, out var rad, out var sc);
        ref var resolved = ref tfStore.Get(id);
        resolved.WorldPosition = pos;
        resolved.WorldRotationRadians = rad;
        resolved.WorldScale = sc;

        processed.TryAdd(id, 0);
    }
}
