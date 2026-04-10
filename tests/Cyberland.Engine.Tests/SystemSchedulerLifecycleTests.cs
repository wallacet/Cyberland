using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Xunit;

namespace Cyberland.Engine.Tests;

public sealed class SystemSchedulerLifecycleTests
{
    private sealed class CountingSeq : ISystem
    {
        public int OnStartCount;
        public int UpdateCount;
        public void OnStart(World world) => OnStartCount++;
        public void OnUpdate(World world, float deltaSeconds) => UpdateCount++;
    }

    private sealed class CountingPar : IParallelSystem
    {
        public int OnStartCount;
        public int UpdateCount;
        public void OnStart(World world) => OnStartCount++;
        public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions) => UpdateCount++;
    }

    [Fact]
    public void RunFrame_OnStart_runs_before_OnUpdate_in_registration_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var log = new List<string>();
        sched.RegisterSequential("a", new LoggingSeq(log, "s1"));
        sched.RegisterParallel("b", new LoggingPar(log, "p1"));
        sched.RegisterSequential("c", new LoggingSeq(log, "s2"));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "s1:start", "s1:up", "p1:start", "p1:up", "s2:start", "s2:up" }, log);
    }

    private sealed class LoggingSeq : ISystem
    {
        private readonly List<string> _log;
        private readonly string _name;

        public LoggingSeq(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void OnStart(World world) => _log.Add(_name + ":start");
        public void OnUpdate(World world, float deltaSeconds) => _log.Add(_name + ":up");
    }

    private sealed class LoggingPar : IParallelSystem
    {
        private readonly List<string> _log;
        private readonly string _name;

        public LoggingPar(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public void OnStart(World world) => _log.Add(_name + ":start");
        public void OnParallelUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions) => _log.Add(_name + ":up");
    }

    [Fact]
    public void Register_disabled_skips_OnStart_and_update_until_SetEnabled()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new CountingSeq();
        sched.RegisterSequential("x", s, enabled: false);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, s.OnStartCount);
        Assert.Equal(0, s.UpdateCount);

        Assert.True(sched.SetEnabled("x", true));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.UpdateCount);
    }

    [Fact]
    public void SetEnabled_false_skips_run_OnStart_not_repeated_on_re_enable()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new CountingSeq();
        sched.RegisterSequential("x", s);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.UpdateCount);

        Assert.True(sched.SetEnabled("x", false));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.UpdateCount);

        Assert.True(sched.SetEnabled("x", true));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(2, s.UpdateCount);
    }

    [Fact]
    public void Replace_resets_OnStart_for_new_instance()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var a = new CountingSeq();
        var b = new CountingSeq();
        sched.RegisterSequential("x", a);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, a.OnStartCount);

        sched.RegisterSequential("x", b);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, a.OnStartCount);
        Assert.Equal(1, b.OnStartCount);
        Assert.Equal(1, b.UpdateCount);
    }

    [Fact]
    public void Unregister_then_register_again_gets_OnStart_again()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var a = new CountingSeq();
        sched.RegisterSequential("x", a);
        sched.RunFrame(new World(), 0.016f);
        Assert.True(sched.TryUnregister("x"));
        var b = new CountingSeq();
        sched.RegisterSequential("x", b);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, b.OnStartCount);
    }

    [Fact]
    public void Events_SystemStarted_SystemEnabled_SystemDisabled_SystemUnregistered()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var started = new List<string>();
        var enabled = new List<string>();
        var disabled = new List<string>();
        var unreg = new List<string>();
        sched.SystemStarted += id => started.Add(id);
        sched.SystemEnabled += id => enabled.Add(id);
        sched.SystemDisabled += id => disabled.Add(id);
        sched.SystemUnregistered += id => unreg.Add(id);

        sched.RegisterSequential("a", new CountingSeq(), enabled: false);
        sched.RunFrame(new World(), 0.016f);
        Assert.Empty(started);

        Assert.True(sched.SetEnabled("a", true));
        Assert.Equal(new[] { "a" }, enabled);

        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(new[] { "a" }, started);

        Assert.True(sched.SetEnabled("a", false));
        Assert.Equal(new[] { "a" }, disabled);

        Assert.True(sched.TryUnregister("a"));
        Assert.Equal(new[] { "a" }, unreg);
    }

    [Fact]
    public void SetEnabled_same_value_does_not_fire_transition_events()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var enabled = 0;
        var disabled = 0;
        sched.SystemEnabled += _ => enabled++;
        sched.SystemDisabled += _ => disabled++;
        sched.RegisterSequential("x", new CountingSeq());
        Assert.True(sched.SetEnabled("x", true));
        Assert.Equal(0, enabled);
        Assert.True(sched.SetEnabled("x", false));
        Assert.Equal(1, disabled);
        Assert.True(sched.SetEnabled("x", false));
        Assert.Equal(1, disabled);
    }

    [Fact]
    public void SetEnabled_unknown_id_returns_false()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.False(sched.SetEnabled("nope", true));
    }

    [Fact]
    public void IsEnabled_and_TryGetEnabled()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.False(sched.IsEnabled("x"));
        Assert.False(sched.TryGetEnabled("x", out _));

        sched.RegisterSequential("x", new CountingSeq(), enabled: false);
        Assert.False(sched.IsEnabled("x"));
        Assert.True(sched.TryGetEnabled("x", out var en) && !en);

        sched.SetEnabled("x", true);
        Assert.True(sched.IsEnabled("x"));
        Assert.True(sched.TryGetEnabled("x", out var en2) && en2);
    }

    [Fact]
    public void SetEnabled_and_IsEnabled_throw_on_invalid_id()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        Assert.Throws<ArgumentException>(() => sched.SetEnabled(" ", true));
        Assert.Throws<ArgumentException>(() => sched.IsEnabled(" "));
        Assert.Throws<ArgumentException>(() => sched.TryGetEnabled(" ", out _));
    }

    [Fact]
    public void Parallel_Register_disabled_then_enable_fires_OnStart_once()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var p = new CountingPar();
        sched.RegisterParallel("p", p, enabled: false);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, p.OnStartCount);

        sched.SetEnabled("p", true);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, p.OnStartCount);
        Assert.Equal(1, p.UpdateCount);
    }

    [Fact]
    public void Replace_parallel_resets_OnStart()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var a = new CountingPar();
        var b = new CountingPar();
        sched.RegisterParallel("p", a);
        sched.RunFrame(new World(), 0.016f);
        sched.RegisterParallel("p", b);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, b.OnStartCount);
    }
}
