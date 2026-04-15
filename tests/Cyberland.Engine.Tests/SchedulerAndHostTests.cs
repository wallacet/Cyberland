using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Input;

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

    [Fact]
    public void ParallelismSettings_CreateParallelOptions_reuses_single_instance()
    {
        var p = new ParallelismSettings { MaxConcurrency = 1 };
        var a = p.CreateParallelOptions();
        var b = p.CreateParallelOptions();
        Assert.Same(a, b);
        p.MaxConcurrency = 3;
        Assert.Equal(3, p.CreateParallelOptions().MaxDegreeOfParallelism);
        Assert.Same(a, p.CreateParallelOptions());
    }

    private sealed class TrackSeq : ISystem, ILateUpdate
    {
        public int Step;
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Step = 1;
        }
    }

    private sealed class TrackPar : IParallelSystem, IParallelLateUpdate
    {
        public int Step;
        public void OnParallelLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions)
        {
            _ = archetype;
            _ = deltaSeconds;
            _ = parallelOptions;
            Step = 2;
        }
    }

    [Fact]
    public void SystemScheduler_runs_in_registration_order_sequential_then_parallel()
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
    public void SystemScheduler_interleaved_entries_run_in_registration_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<int>();
        sched.RegisterSequential("s1", new OrderSeqEarly { Order = order, Mark = 1 });
        sched.RegisterParallel("p1", new OrderParFixed { Order = order, Mark = 2 });
        sched.RegisterSequential("s2", new OrderSeqLate { Order = order, Mark = 3 });
        sched.RegisterParallel("p2", new OrderPar { Order = order, Mark = 4 });
        sched.RunFrame(new World(), 1f / 60f);
        Assert.Equal(new[] { 1, 2, 3, 4 }, order);
    }

    [Fact]
    public void SystemScheduler_RegisterSequential_throws_when_id_empty()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentException>(() => sched.RegisterSequential(" ", new TrackSeq()));
    }

    [Fact]
    public void SystemScheduler_same_id_RegisterParallel_replaces_RegisterSequential()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var seq = new TrackSeq();
        var par = new TrackPar();
        sched.RegisterSequential("shared", seq);
        sched.RegisterParallel("shared", par);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, seq.Step);
        Assert.Equal(2, par.Step);
    }

    [Fact]
    public void SystemScheduler_TryUnregister_removes_entries()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        sched.RegisterSequential("a", new TrackSeq());
        sched.RegisterParallel("b", new TrackPar());
        sched.RegisterSequential("c", new TrackSeq());

        Assert.True(sched.TryUnregister("a"));
        Assert.True(sched.TryUnregister("b"));
        Assert.True(sched.TryUnregister("c"));
        Assert.False(sched.TryUnregister("a"));
    }

    private sealed class OrderSeqEarly : ISystem, IEarlyUpdate
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnEarlyUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add(Mark);
        }
    }

    private sealed class OrderSeqLate : ISystem, ILateUpdate
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds)
        {
            _ = archetype;
            _ = deltaSeconds;
            Order.Add(Mark);
        }
    }

    [Fact]
    public void SystemScheduler_replace_sequential_preserves_run_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var order = new List<int>();
        sched.RegisterSequential("a", new OrderSeqLate { Order = order, Mark = 1 });
        sched.RegisterSequential("b", new OrderSeqLate { Order = order, Mark = 2 });
        sched.RegisterSequential("c", new OrderSeqLate { Order = order, Mark = 3 });
        sched.RegisterSequential("b", new OrderSeqLate { Order = order, Mark = 20 });
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

    private sealed class OrderParFixed : IParallelSystem, IParallelFixedUpdate
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnParallelFixedUpdate(World world, ChunkQueryAll archetype, float fixedDeltaSeconds, ParallelOptions parallelOptions) =>
            Order.Add(Mark);
    }

    private sealed class OrderPar : IParallelSystem, IParallelLateUpdate
    {
        public required List<int> Order { get; init; }
        public required int Mark { get; init; }
        public void OnParallelLateUpdate(World world, ChunkQueryAll archetype, float deltaSeconds, ParallelOptions parallelOptions) =>
            Order.Add(Mark);
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
        Assert.NotNull(host.ParticleEmitterIdsForFrame);
        Assert.Empty(host.ParticleEmitterIdsForFrame);

        Assert.Equal(0f, host.LastPresentDeltaSeconds);
        host.LastPresentDeltaSeconds = 1f / 60f;
        Assert.Equal(1f / 60f, host.LastPresentDeltaSeconds);

        Assert.Equal(0f, host.FixedAccumulatorSeconds);
        host.FixedAccumulatorSeconds = 0.012f;
        Assert.Equal(0.012f, host.FixedAccumulatorSeconds);

        Assert.Equal(1f / 60f, host.FixedDeltaSeconds);
        host.FixedDeltaSeconds = 1f / 120f;
        Assert.Equal(1f / 120f, host.FixedDeltaSeconds);
    }

    [Fact]
    public void GameHostServices_fixed_timing_can_mirror_scheduler_after_RunFrame()
    {
        var sched = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        sched.RunFrame(new World(), 0.01f);
        var host = new GameHostServices(new KeyBindingStore())
        {
            FixedAccumulatorSeconds = sched.FixedAccumulator,
            FixedDeltaSeconds = sched.FixedDeltaSeconds
        };
        Assert.Equal(sched.FixedAccumulator, host.FixedAccumulatorSeconds);
        Assert.Equal(sched.FixedDeltaSeconds, host.FixedDeltaSeconds);
    }
}
