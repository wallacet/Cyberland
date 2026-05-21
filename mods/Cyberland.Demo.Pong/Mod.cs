using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Pong;

/// <summary>
/// Pong sample: one session entity holds <see cref="State"/> + <see cref="Control"/>; sprites/HUD rows are tagged in scene JSON and resolved in systems via <see cref="SceneWire"/>.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/>; <see cref="VisualSyncSystem"/> / <see cref="SimulationSystem"/> resolve tagged entities in <see cref="ISingletonSystem.OnSingletonStart"/>.</para>
/// <para>Input, simulation, lights, and visual sync all use <see cref="ISingletonSystem"/> on the session row (<see cref="SystemQuerySpec.All{State, Control}"/>).</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/pong.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("pong.json");
        KickoffBuiltinAtlasLoads(context);

        var hud = await SetupSceneAsync(context);

        var host = context.Host;
        var strings = context.LocalizedContent.Strings;
        context.RegisterSingleton("cyberland.demo.pong/input", new InputSystem(host, context.Scheduler));
        context.RegisterSingleton("cyberland.demo.pong/simulation", new SimulationSystem(host));
        context.RegisterSingleton("cyberland.demo.pong/lights", new LightsFillSystem(host));
        context.RegisterSingleton("cyberland.demo.pong/visual-sync", new VisualSyncSystem(host, strings, hud));
    }

    /// <inheritdoc />
    public void OnUnload() { }

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap Pong from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Pong scene spawn failed.");

        return ResolveHudRefs(context);
    }

    private static void KickoffBuiltinAtlasLoads(ModLoadContext context)
    {
        ReadOnlySpan<string> manifests =
        [
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular23,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold23,
            BuiltinFonts.BakedAtlasManifestPath.MonoRegular14,
            BuiltinFonts.BakedAtlasManifestPath.MonoRegular18
        ];
        foreach (var path in manifests)
            _ = context.LoadBakedMsdfAtlasAsync(path);
    }
}
