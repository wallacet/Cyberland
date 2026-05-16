using Cyberland.Engine.Assets;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.FontTest;

/// <summary>
/// Font validation demo: built-in atlases plus a custom registered family (Jost) with mod-shipped MSDF bakes.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="FontTestFonts"/> for VFS paths, then private <see cref="SetupSceneAsync"/> and <see cref="Mod.UiDocument"/> for the on-screen matrix.</para>
/// <para><b>Load order inside <see cref="OnLoadAsync"/>:</b> register the custom family first (async I/O to VFS TTFs), then fire-and-forget baked page loads, then spawn scene JSON and build the UI document.</para>
/// <para><b>Do not await <see cref="ModLoadContext.LoadBakedMsdfAtlasAsync"/> here</b> — completion requires the render loop to drain uploads while <see cref="ModLoader"/> still blocks on this method.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/demo_fonttest.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        await RegisterJostFamilyAsync(context).ConfigureAwait(false);
        KickoffAtlasLoadsFireAndForget(context);
        await SetupSceneAsync(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap FontTest from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "FontTest scene spawn failed.");

        BuildFontTestUiDocument(context);
    }

    private static async Task RegisterJostFamilyAsync(ModLoadContext context)
    {
        var assets = new AssetManager(context.VirtualFileSystem);
        await context.Host.Fonts
            .RegisterFamilyFromVirtualPathsAsync(
                assets,
                FontTestFonts.JostFamilyId,
                FontTestFonts.JostRegularVfsPath,
                FontTestFonts.JostBoldVfsPath)
            .ConfigureAwait(false);
    }

    private static void KickoffAtlasLoadsFireAndForget(ModLoadContext context)
    {
        foreach (var path in BuiltinBakedManifestPaths)
            _ = context.LoadBakedMsdfAtlasAsync(path);
        foreach (var path in FontTestFonts.BakedJostManifestVfsPaths)
            _ = context.LoadBakedMsdfAtlasAsync(path);
    }

    private static readonly string[] BuiltinBakedManifestPaths =
    [
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular12,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular13,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular22,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular23,
        BuiltinFonts.BakedAtlasManifestPath.UiSansRegular24,
        BuiltinFonts.BakedAtlasManifestPath.UiSansBold14,
        BuiltinFonts.BakedAtlasManifestPath.UiSansBold18,
        BuiltinFonts.BakedAtlasManifestPath.UiSansBold23,
        BuiltinFonts.BakedAtlasManifestPath.MonoRegular14,
        BuiltinFonts.BakedAtlasManifestPath.MonoRegular18
    ];
}
