using Cyberland.Demo.MouseChase.Systems;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.MouseChase;

/// <summary>
/// Tutorial game: input → fixed simulation (movement, camera zoom, triggers, round state, restart) → retained HUD document updates.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/> and <c>Content/Ui/mousechase_hud.json</c>; systems under <c>Systems/</c>.</para>
/// <para>Single-row drivers use <see cref="ISingletonSystem"/>; <see cref="TriggerResolveSystem"/> stays serial over trigger chunks.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/demo_mousechase.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        MouseChaseInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("mouse_chase.json");
        KickoffBuiltinAtlasLoads(context);

        var hud = await SetupSceneAsync(context);

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

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap Mouse Chase from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Mouse Chase scene spawn failed.");

        return ResolveHudRefs(context);
    }

    private static void KickoffBuiltinAtlasLoads(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
    }
}
