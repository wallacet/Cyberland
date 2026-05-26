using System.Numerics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Structural copy helpers for <see cref="SceneRuntime"/> entity lift.
/// </summary>
public static class EntityWorldTransfer
{
    /// <summary>
    /// Known engine components copied by blit when both worlds support the stores.
    /// </summary>
    public static bool TryCopyEngineComponents(World from, EntityId src, World to, EntityId dst)
    {
        if (from.Has<Transform>(src))
        {
            ref readonly var t = ref from.Get<Transform>(src);
            ref var td = ref to.GetOrAdd<Transform>(dst);
            td = t;
        }

        if (from.Has<LogicalActorId>(src))
        {
            ref readonly var l = ref from.Get<LogicalActorId>(src);
            ref var ld = ref to.GetOrAdd<LogicalActorId>(dst);
            ld = l;
        }

        CopyIfPresent<AmbientLightSource>(from, src, to, dst);
        CopyIfPresent<DirectionalLightSource>(from, src, to, dst);
        CopyIfPresent<SpotLightSource>(from, src, to, dst);
        CopyIfPresent<PointLightSource>(from, src, to, dst);

        return true;
    }

    /// <summary>
    /// Collects entities in <paramref name="sourceWorld"/> whose transform hierarchy is under <paramref name="root"/>.
    /// </summary>
    public static List<EntityId> CollectSubtree(World sourceWorld, EntityId root)
    {
        var result = new List<EntityId>();
        if (!sourceWorld.IsAlive(root))
            return result;

        // Snapshot all transforms and build parent map
        var spec = SystemQuerySpec.All<Transform>();
        foreach (var chunk in sourceWorld.QueryChunks(spec))
        {
            var ents = chunk.Entities;
            var col = chunk.Column<Transform>();
            for (var i = 0; i < ents.Length; i++)
            {
                var id = ents[i];
                if (IsUnderRoot(sourceWorld, id, root))
                    result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// Orders entities parent-before-child so remapped parents exist when creating rows.
    /// </summary>
    public static List<EntityId> OrderParentsBeforeChildren(World world, IReadOnlyList<EntityId> members)
    {
        var set = new HashSet<EntityId>(members);
        var depth = new Dictionary<EntityId, int>();
        foreach (var e in members)
            depth[e] = ComputeDepth(world, e, set);

        return members.OrderBy(e => depth[e]).ToList();
    }

    private static int ComputeDepth(World world, EntityId e, HashSet<EntityId> limit)
    {
        var d = 0;
        var cur = e;
        while (world.TryGet(cur, out Transform t))
        {
            d++;
            var p = t.Parent;
            if (p.Raw == 0)
                break;
            if (!limit.Contains(p))
                break;
            cur = p;
        }

        return d;
    }

    private static void CopyIfPresent<T>(World from, EntityId src, World to, EntityId dst) where T : unmanaged, IComponent
    {
        if (from.Has<T>(src))
        {
            ref readonly var v = ref from.Get<T>(src);
            ref var vd = ref to.GetOrAdd<T>(dst);
            vd = v;
        }
    }

    private static bool IsUnderRoot(World world, EntityId id, EntityId root)
    {
        if (id == root)
            return true;
        var cur = id;
        var guard = 0;
        while (guard++ < 4096)
        {
            if (cur == root)
                return true;
            if (!world.TryGet(cur, out Transform t))
                return false;
            if (t.Parent.Raw == 0)
                return false;
            cur = t.Parent;
        }

        return false;
    }
}
