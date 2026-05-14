using Cyberland.Engine.Modding;

namespace Cyberland.EngineLate;

/// <summary>
/// Registers default engine systems that should run after gameplay mods.
/// </summary>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public ValueTask OnLoadAsync(ModLoadContext context)
    {
        EngineDefaultSchedulerSystems.RegisterAfterGameplayMods(context);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
