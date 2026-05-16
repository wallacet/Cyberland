using Cyberland.Demo.IdleGold.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo.IdleGold;

/// <summary>
/// Idle gold UI showcase: passive income, purchases through retained UI, ECS singleton session row.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for the HUD document and session spawn, then <see cref="UiCommandHandler"/> and <see cref="UiGameCommand"/> records for the command vocabulary.</para>
/// <para><b>Frame flow:</b> UI buttons enqueue into <see cref="Cyberland.Engine.Hosting.GameHostServices.UiCommands"/>; the host drains them into the dispatcher callback assigned here to <see cref="UiCommandHandler.Dispatch"/>; <see cref="SimulationSystem"/> advances economy on the session singleton in **late** update (variable <c>deltaSeconds</c>); <see cref="HudBindSystem"/> mirrors state into labels.</para>
/// <para><b>MSDF bootstrap:</b> synchronous <see cref="LoadBuiltinUiAtlasesForIdleGold"/> uses <see cref="ModLoadContext.LoadBakedMsdfAtlas"/> so first UI frames do not pay runtime MSDF fallback — see **cyberland-demo-mod-authoring**.</para>
/// </remarks>
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
