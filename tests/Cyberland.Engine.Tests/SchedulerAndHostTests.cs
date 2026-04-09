using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class SchedulerAndHostTests
{
    [Fact]
    public void ParallelismSettings_zero_uses_processor_count()
    {
        var p = new ParallelismSettings { MaxConcurrency = 0 };
        var o = p.CreateParallelOptions();
        Assert.Equal(Environment.ProcessorCount, o.MaxDegreeOfParallelism);
    }

    [Fact]
    public void ParallelismSettings_positive_caps_parallelism()
    {
        var p = new ParallelismSettings { MaxConcurrency = 2 };
        Assert.Equal(2, p.CreateParallelOptions().MaxDegreeOfParallelism);
    }

    private sealed class TrackSeq : ISystem
    {
        public int Step;
        public void OnUpdate(World world, float deltaSeconds) => Step = 1;
    }

    private sealed class TrackPar : IParallelSystem
    {
        public int Step;
        public void OnParallelUpdate(World world, ParallelOptions parallelOptions) => Step = 2;
    }

    [Fact]
    public void SystemScheduler_runs_sequential_before_parallel()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new TrackSeq();
        var p = new TrackPar();
        sched.RegisterSequential("test/seq", s);
        sched.RegisterParallel("test/par", p);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.Step);
        Assert.Equal(2, p.Step);
    }

    [Fact]
    public void SystemScheduler_RegisterSequential_throws_when_id_empty()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentException>(() => sched.RegisterSequential(" ", new TrackSeq()));
    }

    [Fact]
    public void SystemScheduler_RegisterParallel_rejects_same_id_as_sequential()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("shared", new TrackSeq());
        Assert.Throws<InvalidOperationException>(() => sched.RegisterParallel("shared", new TrackPar()));
    }

    [Fact]
    public void SystemScheduler_RegisterSequential_rejects_same_id_as_parallel()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterParallel("shared", new TrackPar());
        Assert.Throws<InvalidOperationException>(() => sched.RegisterSequential("shared", new TrackSeq()));
    }

    private sealed class OrderSeq : ISystem
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnUpdate(World world, float deltaSeconds) => Order.Add(Mark);
    }

    [Fact]
    public void SystemScheduler_replace_sequential_preserves_run_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<int>();
        sched.RegisterSequential("a", new OrderSeq { Order = order, Mark = 1 });
        sched.RegisterSequential("b", new OrderSeq { Order = order, Mark = 2 });
        sched.RegisterSequential("c", new OrderSeq { Order = order, Mark = 3 });
        sched.RegisterSequential("b", new OrderSeq { Order = order, Mark = 20 });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { 1, 20, 3 }, order);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_removes_system()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new TrackSeq();
        sched.RegisterSequential("x", s);
        Assert.True(sched.TryUnregister("x"));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, s.Step);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_unknown_returns_false()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.False(sched.TryUnregister("nope"));
    }

    [Fact]
    public void SystemScheduler_TryUnregister_throws_when_id_invalid()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentException>(() => sched.TryUnregister(" "));
    }

    [Fact]
    public void SystemScheduler_RegisterSequential_throws_when_system_null()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentNullException>(() => sched.RegisterSequential("id", null!));
    }

    [Fact]
    public void SystemScheduler_RegisterParallel_throws_when_system_null()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentNullException>(() => sched.RegisterParallel("id", null!));
    }

    private sealed class OrderPar : IParallelSystem
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnParallelUpdate(World world, ParallelOptions parallelOptions) => Order.Add(Mark);
    }

    [Fact]
    public void SystemScheduler_replace_parallel_preserves_run_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<int>();
        sched.RegisterParallel("pa", new OrderPar { Order = order, Mark = 1 });
        sched.RegisterParallel("pb", new OrderPar { Order = order, Mark = 2 });
        sched.RegisterParallel("pc", new OrderPar { Order = order, Mark = 3 });
        sched.RegisterParallel("pb", new OrderPar { Order = order, Mark = 20 });
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { 1, 20, 3 }, order);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_removes_parallel_system()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var p = new TrackPar();
        sched.RegisterParallel("p", p);
        Assert.True(sched.TryUnregister("p"));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, p.Step);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_parallel_rebuilds_when_one_remains()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterParallel("p1", new TrackPar());
        sched.RegisterParallel("p2", new TrackPar());
        Assert.True(sched.TryUnregister("p1"));
        sched.RunFrame(new World(), 0.016f);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_sequential_rebuilds_index_for_multiple_remaining()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("a", new TrackSeq());
        sched.RegisterSequential("b", new TrackSeq());
        sched.RegisterSequential("c", new TrackSeq());
        Assert.True(sched.TryUnregister("b"));
        Assert.True(sched.TryUnregister("a"));
        Assert.False(sched.TryUnregister("a"));
    }

    [Fact]
    public void GameHostServices_exposes_assignable_renderer_and_input()
    {
        var keys = new KeyBindingStore();
        var host = new GameHostServices(keys);
        Assert.Same(keys, host.KeyBindings);
        Assert.Null(host.Renderer);
        Assert.Null(host.Input);
        host.Renderer = null;
        host.Input = null;
    }
}
