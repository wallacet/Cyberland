using Cyberland.Engine.Modding;

namespace Cyberland.Game;

/// <summary>
/// Shipped base campaign mod: locale and future core content. Uses the same <see cref="IMod"/> pipeline as third-party mods.
/// </summary>
public sealed class BaseGameMod : IMod
{
    public ValueTask OnLoadAsync(ModLoadContext context)
    {
        /* Core gameplay systems register here as the campaign grows. */
        return ValueTask.CompletedTask;
    }

    public void OnUnload()
    {
    }
}
