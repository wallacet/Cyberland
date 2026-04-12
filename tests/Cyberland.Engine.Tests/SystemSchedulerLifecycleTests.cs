using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;

namespace Cyberland.Engine.Tests;

public sealed class SystemSchedulerLifecycleTests
{
    private sealed class CountingSeq : ISystem, ILateUpdate
    {
        public int OnStartCount;
        public int LateCount;
        public void OnStart(World world) => OnStartCount++;
        public void OnLateUpdate(World world, float deltaSeconds) => LateCount++;
    }

    private sealed class CountingPar : IParallelSystem, IParallelFixedUpdate
    {
        public int OnStartCount;
        public int FixedCount;
        public void OnStart(World world) => OnStartCount++;
        public void OnParallelFixedUpdate(World world, float fixedDeltaSeconds, ParallelOptions parallelOptions) =>
            FixedCount++;
    }

    [Fact]
    public void RunFrame_OnStart_runs_first_then_Early_Fixed_Late_in_registration_order()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var log = new List<string>();
        sched.RegisterSequential("a", new LoggingSeq(log, "s1", early: true));
        sched.RegisterParallel("b", new LoggingPar(log, "p1", fixedU: true));
        sched.RegisterSequential("c", new LoggingSeq(log, "s2", late: true));
        sched.RunFrame(new World(), 1f / 60f);
        Assert.Equal(new[]
        {
            "s1:start", "p1:start", "s2:start",
            "s1:early",
            "p1:fixed",
            "s2:late"
        }, log);
    }

    [Fact]
    public void RunFrame_invokes_IParallelEarlyUpdate()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var called = false;
        sched.RegisterParallel("pe", new ParEarlyOnly { Callback = () => called = true });
        sched.RunFrame(new World(), 0.016f);
        Assert.True(called);
    }

    private sealed class ParEarlyOnly : IParallelSystem, IParallelEarlyUpdate
    {
        public Action? Callback;

        public void OnParallelEarlyUpdate(World world, float deltaSeconds, ParallelOptions parallelOptions)
        {
            _ = world;
            _ = deltaSeconds;
            _ = parallelOptions;
            Callback?.Invoke();
        }
    }

    private sealed class LoggingSeq : ISystem, IEarlyUpdate, ILateUpdate
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly bool _early;
        private readonly bool _late;

        public LoggingSeq(List<string> log, string name, bool early = false, bool late = false)
        {
            _log = log;
            _name = name;
            _early = early;
            _late = late;
        }

        public void OnStart(World world) => _log.Add(_name + ":start");

        public void OnEarlyUpdate(World world, float deltaSeconds)
        {
            if (_early)
                _log.Add(_name + ":early");
        }

        public void OnLateUpdate(World world, float deltaSeconds)
        {
            if (_late)
                _log.Add(_name + ":late");
        }
    }

    private sealed class LoggingPar : IParallelSystem, IParallelFixedUpdate
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly bool _fixedU;

        public LoggingPar(List<string> log, string name, bool fixedU = false)
        {
            _log = log;
            _name = name;
            _fixedU = fixedU;
        }

        public void OnStart(World world) => _log.Add(_name + ":start");

        public void OnParallelFixedUpdate(World world, float fixedDeltaSeconds, ParallelOptions parallelOptions)
        {
            if (_fixedU)
                _log.Add(_name + ":fixed");
        }
    }

    [Fact]
    public void Register_disabled_skips_OnStart_and_updates_until_SetEnabled()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new CountingSeq();
        sched.RegisterSequential("x", s, enabled: false);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(0, s.OnStartCount);
        Assert.Equal(0, s.LateCount);

        Assert.True(sched.SetEnabled("x", true));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.LateCount);
    }

    [Fact]
    public void SetEnabled_false_skips_run_OnStart_not_repeated_on_re_enable()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var s = new CountingSeq();
        sched.RegisterSequential("x", s);
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.LateCount);

        Assert.True(sched.SetEnabled("x", false));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(1, s.LateCount);

        Assert.True(sched.SetEnabled("x", true));
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, s.OnStartCount);
        Assert.Equal(2, s.LateCount);
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
        Assert.Equal(1, b.LateCount);
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
        sched.RunFrame(new World(), 1f / 60f);
        Assert.Equal(1, p.OnStartCount);
        Assert.Equal(1, p.FixedCount);
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

    [Fact]
    public void Fixed_substeps_and_accumulator_carry()
    {
        var sched = new SystemScheduler(new ParallelismSettings())
        {
            FixedDeltaSeconds = 0.02f,
            MaxSubstepsPerFrame = 10
        };
        var h = new IntHolder();
        sched.RegisterSequential("f", new FixedCounter(h));
        sched.RunFrame(new World(), 0.05f);
        Assert.Equal(2, h.N);
        Assert.InRange(sched.FixedAccumulator, 0.009f, 0.011f);
    }

    private sealed class IntHolder
    {
        public int N;
    }

    private sealed class FixedCounter : ISystem, IFixedUpdate
    {
        private readonly IntHolder _h;
        public FixedCounter(IntHolder h) => _h = h;
        public void OnFixedUpdate(World world, float fixedDeltaSeconds) => _h.N++;
    }

    [Fact]
    public void MaxSubsteps_caps_fixed_calls()
    {
        var sched = new SystemScheduler(new ParallelismSettings())
        {
            FixedDeltaSeconds = 0.02f,
            MaxSubstepsPerFrame = 2
        };
        var h = new IntHolder();
        sched.RegisterSequential("f", new FixedCounter(h));
        sched.RunFrame(new World(), 1f);
        Assert.Equal(2, h.N);
        Assert.True(sched.FixedAccumulator > 0.9f);
    }

    [Fact]
    public void RunFrame_throws_when_FixedDelta_seconds_non_positive()
    {
        var sched = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 0f };
        Assert.Throws<InvalidOperationException>(() => sched.RunFrame(new World(), 0.016f));
    }

    [Fact]
    public void After_events_fire_once_per_frame()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var e = 0;
        var f = 0;
        var l = 0;
        sched.AfterEarlyUpdate += (_, _) => e++;
        sched.AfterFixedUpdate += (_, _) => f++;
        sched.AfterLateUpdate += (_, _) => l++;
        sched.RegisterSequential("x", new CountingSeq());
        sched.RunFrame(new World(), 0.016f);
        Assert.Equal(1, e);
        Assert.Equal(1, f);
        Assert.Equal(1, l);
    }

    [Fact]
    public void RunFrame_sync_callback_receives_accumulator_before_late_phase()
    {
        var sched = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        var accFromCallback = -1f;
        var accSeenInLate = -2f;
        sched.RegisterSequential("capture", new DelegateSequentialSystem(onLateUpdate: (_, _) =>
        {
            accSeenInLate = accFromCallback;
        }));
        sched.RunFrame(new World(), 0.02f, acc => accFromCallback = acc);
        Assert.Equal(sched.FixedAccumulator, accFromCallback);
        Assert.Equal(sched.FixedAccumulator, accSeenInLate);
    }
}
