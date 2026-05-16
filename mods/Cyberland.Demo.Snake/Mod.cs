using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Snake sample: Control + Session + Tilemap + VisualBundle; simulation lives in <see cref="Session.Step"/>.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/>; <see cref="BootstrapSystem"/> allocates segment/HUD entities.</para>
/// <para>Gameplay systems use <see cref="ISingletonSystem"/> for single-row archetypes (session, control, tilemap, visuals).</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/demo_snake.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        SnakeInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("snake.json");
        KickoffBuiltinAtlasLoads(context);

        await SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.snake/bootstrap", new BootstrapSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/input", new InputSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/tick", new TickSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/tilemap-layout", new TilemapLayoutSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/lights", new SnakeLightsFillSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/visual-sync", new VisualSyncSystem(host));
    }

    /// <inheritdoc />
    public void OnUnload() { }

    private static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap Snake from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Snake scene spawn failed.");
    }

    private static void KickoffBuiltinAtlasLoads(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansBold23);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular18);
    }
}
