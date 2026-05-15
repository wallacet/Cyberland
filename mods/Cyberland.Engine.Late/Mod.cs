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
        using var phase = context.BeginLoadPhase("engine-late-register", 1f, "Registering engine late systems");
        EngineDefaultSchedulerSystems.RegisterAfterGameplayMods(context, "engine-late-register");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
