using Cyberland.Demo.IdleGold.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

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
        // Synchronous loads: async atlases complete only after render-thread drain, so first UI frames would miss baked
        // glyphs and fall back to expensive runtime MSDF rasterization (see ModLoadContext.LoadBakedMsdfAtlas remarks).
        LoadBuiltinUiAtlasesForIdleGold(context);

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

    /// <summary>
    /// Seeds the host text glyph cache from engine virtual paths (VFS miss → embedded builtin manifests).
    /// IdleGold does not ship duplicate PNGs under <c>Content/</c>; optional mod overrides can replace these paths.
    /// </summary>
    private static void LoadBuiltinUiAtlasesForIdleGold(ModLoadContext context)
    {
        ReadOnlySpan<string> manifests =
        [
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular13,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold18,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold23,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular22,
            BuiltinFonts.BakedAtlasManifestPath.MonoRegular14
        ];

        foreach (var path in manifests)
            context.LoadBakedMsdfAtlas(path);
    }
}
