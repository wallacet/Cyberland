using Cyberland.Engine;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;

namespace Cyberland.Engine.Tests;

public sealed class SingletonSystemSchedulerTests
{
    private struct SingletonMark : IComponent;

    private struct Payload : IComponent
    {
        public int Value;
    }

    [Fact]
    public void RegisterSingleton_throws_when_QuerySpec_empty()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var ex = Assert.Throws<ArgumentException>(() => sched.RegisterSingleton("s", new EmptySpecSingleton()));
        Assert.Contains("Empty", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisterSingleton_resolves_entity_and_runs_start_and_phases_in_order()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<SingletonMark>(e) = default;
        w.GetOrAdd<Payload>(e) = new Payload { Value = 1 };

        var sched = new SystemScheduler(new ParallelismSettings());
        var log = new List<string>();
        sched.RegisterSerial("before", new MarkStep(log, "seq"));
        sched.RegisterSingleton("single", new FullSingleton(log, w, e));
        sched.RegisterSerial("after", new MarkStep(log, "seq2"));
        sched.RunFrame(w, 1f / 60f);
        Assert.Equal(
            new[]
            {
                "single:start",
                "seq:early",
                "single:early",
                "seq2:early",
                "seq:late",
                "single:late",
                "seq2:late"
            },
            log);
        Assert.Equal(99, w.Get<Payload>(e).Value);
    }

    [Fact]
    public void Singleton_fixed_runs_each_substep()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<SingletonMark>(e) = default;

        var sched = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f, MaxSubstepsPerFrame = 8 };
        var fixedHits = 0;
        sched.RegisterSingleton("fx", new FixedSingleton(() => fixedHits++));
        sched.RunFrame(w, 1f);
        Assert.Equal(8, fixedHits);
    }

    [Fact]
    public void RegisterSingleton_replace_updates_implementation()
    {
        var sched = new SystemScheduler(new ParallelismSettings());
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<SingletonMark>(e) = default;
        var first = false;
        var second = false;
        sched.RegisterSingleton("x", new StartFlagSingleton(() => first = true));
        sched.RegisterSingleton("x", new StartFlagSingleton(() => second = true));
        sched.RunFrame(w, 0.016f);
        Assert.False(first);
        Assert.True(second);
    }

    [Fact]
    public void ModLoadContext_RegisterSingleton_forwards_to_scheduler()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<SingletonMark>(e) = default;
        var sched = new SystemScheduler(new ParallelismSettings());
        var vfs = new VirtualFileSystem();
        var ctx = new ModLoadContext(
            new ModManifest { Id = "t", ContentRoot = "Content" },
            Path.GetTempPath(),
            vfs,
            new LocalizedContent(new LocalizationManager(), vfs, "en"),
            w,
            sched,
            new GameHostServices());
        var hit = false;
        ctx.RegisterSingleton("sg", new StartFlagSingleton(() => hit = true));
        ctx.Scheduler.RunFrame(w, 0.016f);
        Assert.True(hit);
    }

    [Fact]
    public void SingletonEntity_Get_and_TryGet_target_resolved_row()
    {
        var w = new World();
        var e = w.CreateEntity();
        w.GetOrAdd<SingletonMark>(e) = default;
        w.GetOrAdd<Payload>(e) = new Payload { Value = 5 };

        var s = new SingletonEntity(w, e);
        Assert.Equal(e, s.Entity);
        Assert.Same(w, s.World);
        s.Get<Payload>().Value = 7;
        Assert.Equal(7, w.Get<Payload>(e).Value);
        Assert.True(s.TryGet<Payload>(out var p) && p.Value == 7);
    }

    private sealed class EmptySpecSingleton : ISingletonSystem
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;
    }

    private sealed class MarkStep : ISystem, IEarlyUpdate, ILateUpdate
    {
        private readonly List<string> _log;
        private readonly string _id;

        public MarkStep(List<string> log, string id)
        {
            _log = log;
            _id = id;
        }

        public void OnEarlyUpdate(ChunkQueryAll query, float deltaSeconds)
        {
            _ = query;
            _ = deltaSeconds;
            _log.Add($"{_id}:early");
        }

        public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
        {
            _ = query;
            _ = deltaSeconds;
            _log.Add($"{_id}:late");
        }
    }

    private sealed class FixedSingleton : ISingletonSystem, ISingletonFixedUpdate
    {
        private readonly Action _onFixed;

        public FixedSingleton(Action onFixed) => _onFixed = onFixed;

        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SingletonMark>();

        public void OnSingletonFixedUpdate(in SingletonEntity singleton, float fixedDeltaSeconds)
        {
            _ = singleton;
            _ = fixedDeltaSeconds;
            _onFixed();
        }
    }

    private sealed class StartFlagSingleton : ISingletonSystem
    {
        private readonly Action _onStart;

        public StartFlagSingleton(Action onStart) => _onStart = onStart;

        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SingletonMark>();

        public void OnSingletonStart(in SingletonEntity singleton)
        {
            _ = singleton;
            _onStart();
        }
    }

    private sealed class FullSingleton : ISingletonSystem, ISingletonEarlyUpdate, ISingletonLateUpdate
    {
        private readonly List<string> _log;
        private readonly World _expectedWorld;
        private readonly EntityId _expectedEntity;

        public FullSingleton(List<string> log, World expectedWorld, EntityId expectedEntity)
        {
            _log = log;
            _expectedWorld = expectedWorld;
            _expectedEntity = expectedEntity;
        }

        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SingletonMark, Payload>();

        public void OnSingletonStart(in SingletonEntity singleton)
        {
            Assert.Same(_expectedWorld, singleton.World);
            Assert.Equal(_expectedEntity, singleton.Entity);
            _log.Add("single:start");
            singleton.Get<Payload>().Value = 99;
        }

        public void OnSingletonEarlyUpdate(in SingletonEntity singleton, float deltaSeconds)
        {
            _ = deltaSeconds;
            _log.Add("single:early");
            Assert.Equal(99, singleton.Get<Payload>().Value);
        }

        public void OnSingletonLateUpdate(in SingletonEntity singleton, float deltaSeconds)
        {
            _ = deltaSeconds;
            _log.Add("single:late");
            Assert.Equal(99, singleton.Get<Payload>().Value);
        }
    }
}
