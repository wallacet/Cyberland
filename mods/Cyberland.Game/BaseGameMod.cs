using Cyberland.Engine.Modding;

namespace Cyberland.Game;

/// <summary>
/// The shipped campaign uses the same <see cref="IMod"/> pipeline as third-party content.
/// </summary>
public sealed class BaseGameMod : IMod
{
    public ModManifest Manifest { get; } = new()
    {
        Id = "cyberland.base",
        Name = "Cyberland (base game)",
        Version = "0.1.0",
        EntryAssembly = "Cyberland.Game.dll",
        ContentRoot = "Content",
        LoadOrder = 0
    };

    public void OnLoad(ModLoadContext context)
    {
        context.Scheduler.Register(new DemoMoveSystem());
    }

    public void OnUnload()
    {
    }
}
