using Cyberland.Engine.Modding;

namespace Cyberland.Demo;

/// <summary>
/// Optional shipped mod: Vulkan sprite movement + parallel velocity damp ECS sample.
/// Disable by removing <c>Mods/Cyberland.Demo</c> from the host output or lowering load order.
/// </summary>
public sealed class DemoMod : IMod
{
    public ModManifest Manifest { get; } = new()
    {
        Id = "cyberland.demo",
        Name = "Cyberland (Vulkan sprite demo)",
        Version = "0.1.0",
        EntryAssembly = "Cyberland.Demo.dll",
        ContentRoot = "Content",
        LoadOrder = 10
    };

    public void OnLoad(ModLoadContext context)
    {
        var entity = context.World.CreateEntity();
        ref var v = ref context.World.Components<Velocity>().GetOrAdd(entity);
        v = new Velocity { X = 100f, Y = 0f };

        context.Scheduler.Register(new SpriteMoveSystem(context.Host));
        context.Scheduler.Register(new VelocityDampSystem());
    }

    public void OnUnload()
    {
    }
}
