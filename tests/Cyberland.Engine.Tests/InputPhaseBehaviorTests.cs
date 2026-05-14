using System.Collections.Concurrent;
using System.Numerics;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Input;
using Moq;
using Silk.NET.Input;
using System.Collections.Generic;

namespace Cyberland.Engine.Tests;

public sealed class InputPhaseBehaviorTests
{
    [Fact]
    public void InputBehavior_level_reads_are_consistent_across_serial_parallel_singleton_and_phases()
    {
        var pressedKeys = new HashSet<Key> { Key.W };
        var service = CreateService(pressedKeys, () => new Vector2(0f, 0f), () => new[] { new ScrollWheel(0f, 0f) });
        service.Bindings.SetBindings("move", new[] { new InputBinding(InputControl.Keyboard(Key.W)) });

        var world = new World();
        var marker = world.CreateEntity();
        world.GetOrAdd<ProbeTag>(marker);
        var singleton = world.CreateEntity();
        world.GetOrAdd<ProbeSingletonTag>(singleton);

        var scheduler = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        var observations = new ConcurrentBag<(string Id, bool Down, float Axis)>();

        scheduler.RegisterSerial("probe/serial-early", new SerialProbeEarly(service, observations));
        scheduler.RegisterSerial("probe/serial-fixed", new SerialProbeFixed(service, observations));
        scheduler.RegisterSerial("probe/serial-late", new SerialProbeLate(service, observations));
        scheduler.RegisterParallel("probe/parallel-early", new ParallelProbeEarly(service, observations));
        scheduler.RegisterParallel("probe/parallel-fixed", new ParallelProbeFixed(service, observations));
        scheduler.RegisterParallel("probe/parallel-late", new ParallelProbeLate(service, observations));
        scheduler.RegisterSingleton("probe/singleton-early", new SingletonProbeEarly(service, observations));
        scheduler.RegisterSingleton("probe/singleton-fixed", new SingletonProbeFixed(service, observations));
        scheduler.RegisterSingleton("probe/singleton-late", new SingletonProbeLate(service, observations));

        service.BeginFrame();
        scheduler.RunFrame(world, 1f / 60f);

        Assert.Equal(9, observations.Count);
        foreach (var (_, down, axis) in observations)
        {
            Assert.True(down);
            Assert.Equal(1f, axis);
        }
    }

    [Fact]
    public void InputBehavior_HasActionPressedThisFrame_matches_across_singleton_early_and_fixed_same_tick()
    {
        Action<IKeyboard, Key, int>? onKeyDown = null;
        var keyboard = new Mock<IKeyboard>(MockBehavior.Loose);
        keyboard.Setup(k => k.IsKeyPressed(It.IsAny<Key>())).Returns(false);
        keyboard.SetupAdd(k => k.KeyDown += It.IsAny<Action<IKeyboard, Key, int>>())
            .Callback((Action<IKeyboard, Key, int> h) => onKeyDown = h);
        keyboard.SetupRemove(k => k.KeyDown -= It.IsAny<Action<IKeyboard, Key, int>>());

        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(new[] { keyboard.Object });
        input.SetupGet(x => x.Mice).Returns(Array.Empty<IMouse>());
        input.Setup(x => x.Dispose());
        var service = new SilkInputService(input.Object);
        service.Bindings.SetBindings("fire", new[] { new InputBinding(InputControl.Keyboard(Key.Space)) });

        var world = new World();
        var singleton = world.CreateEntity();
        world.GetOrAdd<ProbeSingletonTag>(singleton);

        var scheduler = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        var hits = new List<bool>();
        scheduler.RegisterSingleton("probe/frame-cmd", new SingletonFrameCmdProbe(service, hits));

        Assert.NotNull(onKeyDown);
        onKeyDown!(keyboard.Object, Key.Space, 0);
        service.BeginFrame();
        scheduler.RunFrame(world, 1f / 60f);

        Assert.Equal(2, hits.Count);
        Assert.All(hits, Assert.True);

        service.Dispose();
    }

    [Fact]
    public void InputBehavior_consume_pressed_survives_zero_fixed_substeps_for_each_system_type()
    {
        AssertConsumePressedSurvivesZeroSubsteps(SystemKind.Serial);
        AssertConsumePressedSurvivesZeroSubsteps(SystemKind.Parallel);
        AssertConsumePressedSurvivesZeroSubsteps(SystemKind.Singleton);
    }

    [Fact]
    public void InputBehavior_consume_axis_delta_survives_zero_fixed_substeps_for_each_system_type()
    {
        AssertConsumeAxisDeltaSurvivesZeroSubsteps(SystemKind.Serial);
        AssertConsumeAxisDeltaSurvivesZeroSubsteps(SystemKind.Parallel);
        AssertConsumeAxisDeltaSurvivesZeroSubsteps(SystemKind.Singleton);
    }

    private static void AssertConsumePressedSurvivesZeroSubsteps(SystemKind kind)
    {
        Action<IKeyboard, Key, int>? onKeyDown = null;
        var keyboard = new Mock<IKeyboard>(MockBehavior.Loose);
        keyboard.Setup(k => k.IsKeyPressed(It.IsAny<Key>())).Returns(false);
        keyboard.SetupAdd(k => k.KeyDown += It.IsAny<Action<IKeyboard, Key, int>>())
            .Callback((Action<IKeyboard, Key, int> h) => onKeyDown = h);
        keyboard.SetupRemove(k => k.KeyDown -= It.IsAny<Action<IKeyboard, Key, int>>());

        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(new[] { keyboard.Object });
        input.SetupGet(x => x.Mice).Returns(Array.Empty<IMouse>());
        input.Setup(x => x.Dispose());
        var service = new SilkInputService(input.Object);
        service.Bindings.SetBindings("fire", new[] { new InputBinding(InputControl.Keyboard(Key.Space)) });

        var world = new World();
        var entity = world.CreateEntity();
        world.GetOrAdd<ProbeTag>(entity);
        var singleton = world.CreateEntity();
        world.GetOrAdd<ProbeSingletonTag>(singleton);

        var scheduler = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        var probe = new ConsumePressProbe(service);
        RegisterConsumeProbe(scheduler, kind, probe);

        Assert.NotNull(onKeyDown);
        onKeyDown!(keyboard.Object, Key.Space, 0);

        service.BeginFrame();
        scheduler.RunFrame(world, 0.001f); // zero fixed substeps
        Assert.False(probe.Consumed);

        service.BeginFrame();
        scheduler.RunFrame(world, 1f / 60f); // fixed now runs and must see pending event
        Assert.True(probe.Consumed);
    }

    private static void AssertConsumeAxisDeltaSurvivesZeroSubsteps(SystemKind kind)
    {
        var pressedKeys = new HashSet<Key>();
        var wheel = new[] { new ScrollWheel(0f, 0f) };
        var service = CreateService(pressedKeys, () => new Vector2(0f, 0f), () => wheel);
        service.Bindings.SetBindings("zoom", new[] { new InputBinding(InputControl.MouseAxisControl(MouseAxis.WheelY)) });

        var world = new World();
        var entity = world.CreateEntity();
        world.GetOrAdd<ProbeTag>(entity);
        var singleton = world.CreateEntity();
        world.GetOrAdd<ProbeSingletonTag>(singleton);

        var scheduler = new SystemScheduler(new ParallelismSettings()) { FixedDeltaSeconds = 1f / 60f };
        var probe = new ConsumeAxisProbe(service);
        RegisterConsumeProbe(scheduler, kind, probe);

        wheel = new[] { new ScrollWheel(0f, -1f) };
        service.BeginFrame();
        scheduler.RunFrame(world, 0.001f); // zero fixed substeps
        Assert.Equal(0f, probe.TotalConsumed);

        wheel = new[] { new ScrollWheel(0f, 0f) };
        service.BeginFrame();
        scheduler.RunFrame(world, 1f / 60f);
        Assert.Equal(-1f, probe.TotalConsumed);
    }

    private static void RegisterConsumeProbe(SystemScheduler scheduler, SystemKind kind, IConsumeProbe probe)
    {
        switch (kind)
        {
            case SystemKind.Serial:
                scheduler.RegisterSerial("probe/consume-serial", new SerialConsumeProbe(probe));
                break;
            case SystemKind.Parallel:
                scheduler.RegisterParallel("probe/consume-parallel", new ParallelConsumeProbe(probe));
                break;
            case SystemKind.Singleton:
                scheduler.RegisterSingleton("probe/consume-singleton", new SingletonConsumeProbe(probe));
                break;
        }
    }

    private static SilkInputService CreateService(
        HashSet<Key> pressedKeys,
        Func<Vector2> mousePosition,
        Func<IReadOnlyList<ScrollWheel>> mouseWheels)
    {
        var keyboard = new Mock<IKeyboard>(MockBehavior.Loose);
        keyboard.Setup(x => x.IsKeyPressed(It.IsAny<Key>())).Returns<Key>(key => pressedKeys.Contains(key));

        var mouse = new Mock<IMouse>(MockBehavior.Strict);
        mouse.Setup(x => x.IsButtonPressed(It.IsAny<MouseButton>())).Returns(false);
        mouse.SetupGet(x => x.Position).Returns(() => mousePosition());
        mouse.SetupGet(x => x.ScrollWheels).Returns(() => mouseWheels());
        mouse.SetupAdd(x => x.MouseDown += It.IsAny<Action<IMouse, MouseButton>>());
        mouse.SetupRemove(x => x.MouseDown -= It.IsAny<Action<IMouse, MouseButton>>());
        mouse.SetupAdd(x => x.MouseUp += It.IsAny<Action<IMouse, MouseButton>>());
        mouse.SetupRemove(x => x.MouseUp -= It.IsAny<Action<IMouse, MouseButton>>());

        var input = new Mock<IInputContext>(MockBehavior.Strict);
        input.SetupGet(x => x.Keyboards).Returns(new[] { keyboard.Object });
        input.SetupGet(x => x.Mice).Returns(new[] { mouse.Object });
        input.Setup(x => x.Dispose());
        return new SilkInputService(input.Object);
    }

    private enum SystemKind
    {
        Serial,
        Parallel,
        Singleton
    }

    private struct ProbeTag : IComponent { }
    private struct ProbeSingletonTag : IComponent { }

    private sealed class SerialProbeEarly(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISystem, IEarlyUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnEarlyUpdate(ChunkQueryAll query, float deltaSeconds)
        {
            _ = query; _ = deltaSeconds;
            observations.Add(("serial-early", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class SerialProbeFixed(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISystem, IFixedUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
        {
            _ = query; _ = fixedDeltaSeconds;
            observations.Add(("serial-fixed", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class SerialProbeLate(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISystem, ILateUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
        {
            _ = query; _ = deltaSeconds;
            observations.Add(("serial-late", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class ParallelProbeEarly(IInputService input, ConcurrentBag<(string, bool, float)> observations) : IParallelSystem, IParallelEarlyUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnParallelEarlyUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
        {
            _ = query; _ = deltaSeconds; _ = parallelOptions;
            observations.Add(("parallel-early", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class ParallelProbeFixed(IInputService input, ConcurrentBag<(string, bool, float)> observations) : IParallelSystem, IParallelFixedUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
        {
            _ = query; _ = fixedDeltaSeconds; _ = parallelOptions;
            observations.Add(("parallel-fixed", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class ParallelProbeLate(IInputService input, ConcurrentBag<(string, bool, float)> observations) : IParallelSystem, IParallelLateUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
        {
            _ = query; _ = deltaSeconds; _ = parallelOptions;
            observations.Add(("parallel-late", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class SingletonProbeEarly(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISingletonSystem, ISingletonEarlyUpdate
    {
        public string SingletonLabel => "probe singleton";
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeSingletonTag>();
        public void OnSingletonStart(in SingletonEntity row) => _ = row;
        public void OnSingletonEarlyUpdate(in SingletonEntity row, float deltaSeconds)
        {
            _ = row; _ = deltaSeconds;
            observations.Add(("singleton-early", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class SingletonProbeFixed(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISingletonSystem, ISingletonFixedUpdate
    {
        public string SingletonLabel => "probe singleton";
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeSingletonTag>();
        public void OnSingletonStart(in SingletonEntity row) => _ = row;
        public void OnSingletonFixedUpdate(in SingletonEntity row, float fixedDeltaSeconds)
        {
            _ = row; _ = fixedDeltaSeconds;
            observations.Add(("singleton-fixed", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private sealed class SingletonProbeLate(IInputService input, ConcurrentBag<(string, bool, float)> observations) : ISingletonSystem, ISingletonLateUpdate
    {
        public string SingletonLabel => "probe singleton";
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeSingletonTag>();
        public void OnSingletonStart(in SingletonEntity row) => _ = row;
        public void OnSingletonLateUpdate(in SingletonEntity row, float deltaSeconds)
        {
            _ = row; _ = deltaSeconds;
            observations.Add(("singleton-late", input.IsDown("move"), input.ReadAxis("move")));
        }
    }

    private interface IConsumeProbe
    {
        void Run();
    }

    private sealed class ConsumePressProbe(IInputService input) : IConsumeProbe
    {
        public bool Consumed { get; private set; }
        public void Run() => Consumed = Consumed || input.ConsumePressed("fire");
    }

    private sealed class ConsumeAxisProbe(IInputService input) : IConsumeProbe
    {
        public float TotalConsumed { get; private set; }
        public void Run() => TotalConsumed += input.ConsumeAxisDelta("zoom");
    }

    private sealed class SerialConsumeProbe(IConsumeProbe probe) : ISystem, IFixedUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds) { _ = query; _ = fixedDeltaSeconds; probe.Run(); }
    }

    private sealed class ParallelConsumeProbe(IConsumeProbe probe) : IParallelSystem, IParallelFixedUpdate
    {
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeTag>();
        public void OnStart(World world, ChunkQueryAll query) { _ = world; _ = query; }
        public void OnParallelFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds, ParallelOptions parallelOptions)
        {
            _ = query; _ = fixedDeltaSeconds; _ = parallelOptions; probe.Run();
        }
    }

    private sealed class SingletonConsumeProbe(IConsumeProbe probe) : ISingletonSystem, ISingletonFixedUpdate
    {
        public string SingletonLabel => "probe singleton";
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeSingletonTag>();
        public void OnSingletonStart(in SingletonEntity row) => _ = row;
        public void OnSingletonFixedUpdate(in SingletonEntity row, float fixedDeltaSeconds) { _ = row; _ = fixedDeltaSeconds; probe.Run(); }
    }

    private sealed class SingletonFrameCmdProbe(IInputService input, List<bool> hits) : ISingletonSystem, ISingletonEarlyUpdate, ISingletonFixedUpdate
    {
        public string SingletonLabel => "probe singleton";
        public SystemQuerySpec QuerySpec => SystemQuerySpec.All<ProbeSingletonTag>();
        public void OnSingletonStart(in SingletonEntity row) => _ = row;

        public void OnSingletonEarlyUpdate(in SingletonEntity row, float deltaSeconds)
        {
            _ = row;
            _ = deltaSeconds;
            hits.Add(input.HasActionPressedThisFrame("fire"));
        }

        public void OnSingletonFixedUpdate(in SingletonEntity row, float fixedDeltaSeconds)
        {
            _ = row;
            _ = fixedDeltaSeconds;
            hits.Add(input.HasActionPressedThisFrame("fire"));
        }
    }
}
