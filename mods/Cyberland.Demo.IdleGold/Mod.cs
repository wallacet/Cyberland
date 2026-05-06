using Cyberland.Demo.IdleGold.Systems;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo.IdleGold;

/// <summary>
/// Idle gold UI showcase: passive income, purchases through retained UI, ECS singleton session row.
/// </summary>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTable("idlegold.json");

        var boot = await SceneSetup.SetupSceneAsync(context);

        var host = context.Host;
        host.UiCommandDispatcher = cmd =>
            UiCommandHandler.Dispatch(context.World, boot.SessionEntity, context.LocalizedContent.Strings, cmd);

        context.RegisterSingleton("cyberland.demo.idlegold/simulation",
            new SimulationSystem(context.LocalizedContent.Strings));
        context.RegisterSingleton("cyberland.demo.idlegold/hud-bind",
            new HudBindSystem(boot.Refs, context.LocalizedContent.Strings, host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
