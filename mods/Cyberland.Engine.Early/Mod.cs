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
        using var phase = context.BeginLoadPhase("engine-early-register", 1f, "Registering engine early systems");
        EngineDefaultSchedulerSystems.RegisterBeforeGameplayMods(context, "engine-early-register");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
