using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene2D.Systems;

/// <summary>
/// Resolves <see cref="Transform"/> hierarchies into world <see cref="Position"/>, <see cref="Rotation"/>, <see cref="Scale"/>.
/// Forest roots are solved in parallel (disjoint subtrees); cycle leftovers are resolved sequentially.
/// </summary>
public sealed class TransformHierarchySystem : IParallelSystem
{
    private readonly List<EntityId> _order = new();
    private readonly Queue<EntityId> _queue = new();
    private readonly Dictionary<EntityId, List<EntityId>> _children = new();
    private readonly HashSet<EntityId> _tfSet = new();
    private readonly HashSet<EntityId> _visited = new();
    private readonly Dictionary<EntityId, Matrix3x2> _world = new();
    private readonly List<EntityId> _roots = new();

    public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
    {
        _ = deltaSeconds;
        _children.Clear();
        _tfSet.Clear();
        _order.Clear();
        _queue.Clear();
        _visited.Clear();
        _world.Clear();
        _roots.Clear();

        var tfStore = world.Components<Transform>();
        foreach (var view in world.QueryChunks<Transform>())
        {
            var ents = view.Entities;
            for (var i = 0; i < view.Count; i++)
                _tfSet.Add(ents[i]);
        }

        var inDegree = new Dictionary<EntityId, int>();
        foreach (var id in _tfSet)
            inDegree[id] = 0;

        foreach (var id in _tfSet)
        {
            ref readonly var t = ref tfStore.Get(id);
            var p = t.Parent;
            if (p.Raw == 0 || !world.IsAlive(p) || !_tfSet.Contains(p))
                continue;

            if (!_children.TryGetValue(p, out var list))
            {
                list = new List<EntityId>();
                _children[p] = list;
            }

            list.Add(id);
            inDegree[id]++;
        }

        foreach (var id in _tfSet)
        {
            if (inDegree[id] == 0)
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
                inDegree[c]--;
                if (inDegree[c] == 0)
                    _queue.Enqueue(c);
            }
        }

        foreach (var id in _tfSet)
        {
            if (!_visited.Contains(id))
                _order.Add(id);
        }

        var processed = new ConcurrentDictionary<EntityId, byte>();
        if (_roots.Count > 0)
        {
            Parallel.ForEach(_roots, parallelOptions, root =>
            {
                var localW = new Dictionary<EntityId, Matrix3x2>();
                var subOrder = BuildSubtreeBfsOrder(root);
                foreach (var eid in subOrder)
                    SolveOne(world, tfStore, eid, localW, processed);
            });
        }

        foreach (var id in _order)
        {
            if (processed.ContainsKey(id))
                continue;

            // Same parent resolution as parallel SolveOne, using the shared world matrix map built for this frame.
            SolveOne(world, tfStore, id, _world, processed);
        }
    }

    private List<EntityId> BuildSubtreeBfsOrder(EntityId root)
    {
        var order = new List<EntityId>();
        var q = new Queue<EntityId>();
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
        var local = Transform2DMath.LocalMatrix(in t);
        Matrix3x2 parentM;
        var p = t.Parent;
        if (p.Raw == 0 || !world.IsAlive(p))
            parentM = Matrix3x2.Identity;
        else if (localW.TryGetValue(p, out var pm))
            parentM = pm;
        else
            parentM = StaticBasisMatrix(world, p);

        var wM = Transform2DMath.Compose(parentM, local);
        localW[id] = wM;
        Transform2DMath.DecomposeToPRS(wM, out var pos, out var rad, out var sc);

        ref var wp = ref world.Components<Position>().GetOrAdd(id);
        wp.X = pos.X;
        wp.Y = pos.Y;
        ref var wr = ref world.Components<Rotation>().GetOrAdd(id);
        wr.Radians = rad;
        ref var ws = ref world.Components<Scale>().GetOrAdd(id);
        ws.X = sc.X;
        ws.Y = sc.Y;

        processed.TryAdd(id, 0);
    }

    private static Matrix3x2 StaticBasisMatrix(World world, EntityId id)
    {
        var pos = world.Components<Position>().TryGet(id, out var p) ? p : default;
        var rot = world.Components<Rotation>().TryGet(id, out var r) ? r.Radians : 0f;
        var sc = world.Components<Scale>().TryGet(id, out var s) ? s : Scale.One;
        return Transform2DMath.MatrixFromPositionRotationScale(pos.AsVector(), rot, sc.AsVector());
    }
}
