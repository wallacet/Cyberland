using Cyberland.Engine.Modding;

namespace Cyberland.Game;

/// <summary>
/// Shipped base campaign mod: locale and future core content. Uses the same <see cref="IMod"/> pipeline as third-party mods.
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
        /* Core gameplay systems register here as the campaign grows. */
    }

    public void OnUnload()
    {
    }
}
