using Cyberland.Demo.MouseChase.Systems;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo.MouseChase;

/// <summary>
/// Tutorial game: input → fixed simulation (movement, camera zoom, triggers, round state, restart) → retained HUD document updates.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for entities and HUD tags; this file lists scheduler registration only.</para>
/// <para>Single-row drivers use <see cref="ISingletonSystem"/>; <see cref="TriggerResolveSystem"/> stays serial over trigger chunks.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        MouseChaseInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("mouse_chase.json");

        var hud = await SceneSetup.SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.mousechase/input", new InputSystem(host));
        context.RegisterSingleton("cyberland.demo.mousechase/reset", new RoundResetSystem());
        context.RegisterSingleton("cyberland.demo.mousechase/movement", new PlayerMovementSystem(host));
        context.RegisterSingleton("cyberland.demo.mousechase/camera-zoom", new CameraZoomSystem(host));
        context.RegisterSerial("cyberland.demo.mousechase/trigger-resolve", new TriggerResolveSystem());
        context.RegisterSingleton("cyberland.demo.mousechase/round-state", new RoundStateSystem());
        context.RegisterSingleton("cyberland.demo.mousechase/hud-ui",
            new HudUiSystem(context.LocalizedContent.Strings, host, hud));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
