using Cyberland.Demo.WhackAMole.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.WhackAMole;

/// <summary>
/// Whack-a-Mole sample: one target square at a time, score-on-click, one-minute countdown after first hit.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/>; <see cref=""/> resolves tagged rows in <see cref="ISingletonSystem.OnSingletonStart"/>.</para>
/// <para><b>MSDF:</b> synchronous <see cref="SeedHudMsdfAtlases"/> matches the IdleGold pattern (guaranteed glyphs before first frame).</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/whackamole.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        SeedHudMsdfAtlases(context);

        var hud = await SetupSceneAsync(context).ConfigureAwait(false);
        context.RegisterSingleton("cyberland.demo.whackamole/game", new GameSystem(context.Host, hud));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap Whack-a-Mole from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Whack-a-Mole scene spawn failed.");

        return ResolveHudRefs(context);
    }

    private static void SeedHudMsdfAtlases(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16);
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20);
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular24);
    }
}
