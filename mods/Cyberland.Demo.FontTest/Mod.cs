using Cyberland.Engine.Assets;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo.FontTest;

/// <summary>
/// Font validation demo: built-in atlases plus a custom registered family (Jost) with mod-shipped MSDF bakes.
/// </summary>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        await RegisterJostFamilyAsync(context).ConfigureAwait(false);
        // Do not await LoadBakedMsdfAtlasAsync: completion is tied to render-thread DrainPendingUploads, while
        // ModLoader.LoadAll blocks on OnLoadAsync until the first frame — awaiting would deadlock startup.
        KickoffAtlasLoadsFireAndForget(context);
        await SceneSetup.SetupSceneAsync(context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void OnUnload()
    {
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
