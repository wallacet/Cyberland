using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Tests;

public sealed class SystemSchedulerOrderingTests
{
    [Fact]
    public void RunAfterAttribute_throws_when_target_empty()
    {
        Assert.Throws<ArgumentException>(() => new RunAfterAttribute(" "));
    }

    [Fact]
    public void RunBeforeAttribute_throws_when_target_empty()
    {
        Assert.Throws<ArgumentException>(() => new RunBeforeAttribute(" "));
    }

    private sealed class LateMark : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public required string Name { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add(Name);
        }
    }

    [RunAfter("ordering/a")]
    private sealed class LateAfterA : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add("c");
        }
    }

    [Fact]
    public void SystemScheduler_RunAfter_moves_system_after_target_when_registered_first()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.RegisterSequential("ordering/c", new LateAfterA { Order = order });
        sched.RegisterSequential("ordering/a", new LateMark { Order = order, Name = "a" });
        sched.RegisterSequential("ordering/b", new LateMark { Order = order, Name = "b" });
        sched.RunFrame(new World(), 0.016f);
        /* c has RunAfter(a) only; among ready nodes after a, tie-break uses registration ordinal (c before b). */
        Assert.Equal(new[] { "a", "c", "b" }, order);
    }

    [RunAfter("orderingLate/engine")]
    private sealed class ModLate : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add("mod");
        }
    }

    [Fact]
    public void SystemScheduler_RunAfter_resolves_when_target_registers_later()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.RegisterSequential("orderingLate/mod", new ModLate { Order = order });
        sched.RegisterSequential("orderingLate/engine", new LateMark { Order = order, Name = "eng" });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "eng", "mod" }, order);
    }

    [RunBefore("orderingBefore/b")]
    private sealed class LateBeforeB : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add("a");
        }
    }

    [Fact]
    public void SystemScheduler_RunBefore_orders_before_target()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.RegisterSequential("orderingBefore/a", new LateBeforeB { Order = order });
        sched.RegisterSequential("orderingBefore/b", new LateMark { Order = order, Name = "b" });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "a", "b" }, order);
    }

    [RunAfter("cycle/b")]
    private sealed class CycleA : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [RunAfter("cycle/a")]
    private sealed class CycleB : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [Fact]
    public void SystemScheduler_RunAfter_cycle_throws_when_graph_is_complete()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("cycle/a", new CycleA());
        Assert.Throws<InvalidOperationException>(() => sched.RegisterSequential("cycle/b", new CycleB()));
    }

    [RunAfter("orphan/missing")]
    private sealed class OrphanRef : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [Fact]
    public void SystemScheduler_unknown_constraint_target_throws_on_first_RunFrame()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("orphan/x", new OrphanRef());
        Assert.Throws<InvalidOperationException>(() => sched.RunFrame(new World(), 0.016f));
    }

    [Fact]
    public void SystemScheduler_EndDeferExecutionOrderRebuilds_without_Begin_throws()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<InvalidOperationException>(() => sched.EndDeferExecutionOrderRebuilds());
    }

    [Fact]
    public void SystemScheduler_DeferExecutionOrderRebuilds_disposable_pairs_begin_end()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        using (sched.DeferExecutionOrderRebuilds())
        {
            sched.RegisterSequential("d1", new LateMark { Order = new List<string>(), Name = "x" });
        }

        sched.RunFrame(new World(), 0.016f);
    }

    [Fact]
    public void SystemScheduler_nested_defer_depth_requires_matching_ends()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.BeginDeferExecutionOrderRebuilds();
        sched.BeginDeferExecutionOrderRebuilds();
        sched.RegisterSequential("n1", new LateMark { Order = new List<string>(), Name = "a" });
        sched.EndDeferExecutionOrderRebuilds();
        sched.EndDeferExecutionOrderRebuilds();
        sched.RunFrame(new World(), 0.016f);
    }

    [RunAfter("deferPair/a")]
    private sealed class DeferAfterA : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add("b");
        }
    }

    [Fact]
    public void SystemScheduler_defer_delays_rebuild_until_end()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.BeginDeferExecutionOrderRebuilds();
        sched.RegisterSequential("deferPair/b", new DeferAfterA { Order = order });
        sched.RegisterSequential("deferPair/a", new LateMark { Order = order, Name = "a" });
        sched.EndDeferExecutionOrderRebuilds();
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "a", "b" }, order);
    }

    [RunAfter("rep/a")]
    private sealed class V1 : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    private sealed class V2 : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [Fact]
    public void SystemScheduler_replace_removes_RunAfter_constraints()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("rep/b", new V1());
        sched.RegisterSequential("rep/a", new LateMark { Order = new List<string>(), Name = "a" });
        sched.RegisterSequential("rep/b", new V2());
        sched.RunFrame(new World(), 0.016f);
    }

    private sealed class ParLate : IParallelSystem, IParallelLateUpdate
    {
        public required List<string> Order { get; init; }
        public required string Name { get; init; }
        public void OnParallelLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions) =>
            Order.Add(Name);
    }

    [RunAfter("parOrd/a")]
    private sealed class ParAfterA : IParallelSystem, IParallelLateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnParallelLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions) =>
            Order.Add("b");
    }

    [Fact]
    public void SystemScheduler_RunAfter_works_on_parallel_systems()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.RegisterParallel("parOrd/b", new ParAfterA { Order = order });
        sched.RegisterParallel("parOrd/a", new ParLate { Order = order, Name = "a" });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "a", "b" }, order);
    }

    [RunAfter("mix/a")]
    [RunAfter("mix/b")]
    private sealed class MultiAfter : ISystem, ILateUpdate
    {
        public required List<string> Order { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add("c");
        }
    }

    [Fact]
    public void SystemScheduler_multiple_RunAfter_on_same_class()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<string>();
        sched.RegisterSequential("mix/c", new MultiAfter { Order = order });
        sched.RegisterSequential("mix/a", new LateMark { Order = order, Name = "a" });
        sched.RegisterSequential("mix/b", new LateMark { Order = order, Name = "b" });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "a", "b", "c" }, order);
    }

    [Fact]
    public void SystemScheduler_first_RunFrame_validates_RunBefore_orphan()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("bf/x", new OrphanBefore());
        Assert.Throws<InvalidOperationException>(() => sched.RunFrame(new World(), 0.016f));
    }

    [RunBefore("orphanBefore/nope")]
    private sealed class OrphanBefore : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    /* Two identical RunAfter attributes produce duplicate edges; scheduler dedupes via continue. */
    [RunAfter("dupEdge/a")]
    [RunAfter("dupEdge/a")]
    private sealed class DupRunAfter : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [Fact]
    public void SystemScheduler_duplicate_identical_RunAfter_edges_are_deduped()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("dupEdge/b", new DupRunAfter());
        sched.RegisterSequential("dupEdge/a", new LateMark { Order = new List<string>(), Name = "a" });
        sched.RunFrame(new World(), 0.016f);
    }

    [RunBefore("rbf/b")]
    private sealed class RunBeforeNeedsB : ISystem, ILateUpdate
    {
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds) { }
    }

    [Fact]
    public void SystemScheduler_RunBefore_defers_rebuild_until_target_registered()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("rbf/a", new RunBeforeNeedsB());
        sched.RegisterSequential("rbf/b", new LateMark { Order = new List<string>(), Name = "b" });
        sched.RunFrame(new World(), 0.016f);
    }
}
