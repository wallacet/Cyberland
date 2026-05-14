using Cyberland.Engine.Modding;

namespace Cyberland.EngineEarly;

/// <summary>
/// Registers default engine systems that should run before gameplay mods.
/// </summary>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public ValueTask OnLoadAsync(ModLoadContext context)
    {
        EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods(context);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
