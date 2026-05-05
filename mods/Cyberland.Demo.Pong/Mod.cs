using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Pong sample: one session entity holds <see cref="State"/> + <see cref="Control"/>; sprites/HUD use <see cref="VisualIds"/> / <see cref="HudTextIds"/> from <see cref="SceneSetup"/>.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for authored entities; registration order below matches frame dependencies.</para>
/// <para>Input, simulation, lights, and visual sync all use <see cref="ISingletonSystem"/> on the session row (<see cref="SystemQuerySpec.All{State, Control}"/>).</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        PongInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("pong.json");

        var scene = await SceneSetup.SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.pong/input", new InputSystem(host, context.Scheduler));
        context.RegisterSingleton("cyberland.demo.pong/simulation", new SimulationSystem(host, scene.Visuals));
        context.RegisterSingleton("cyberland.demo.pong/lights", new PongLightsFillSystem(host));
        context.RegisterSingleton("cyberland.demo.pong/visual-sync", new VisualSyncSystem(host, scene.Visuals, scene.Texts));
    }

    /// <inheritdoc />
    public void OnUnload() { }
}
