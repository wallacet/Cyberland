using Cyberland.Demo.SpriteGallery.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.SpriteGallery;

/// <summary>
/// Visual atlas/texture feature gallery: static regions, animations, sheets, locale overlays, missing-texture fallbacks, and 9-slice UI.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="Mod.UiBind.cs"/> + <see cref="HudDocumentRefs"/> + <see cref="Systems.HudUiSystem"/> (FPS only); <see cref="SceneComponentDeserializers"/>; <see cref="ScenePath"/>.</para>
/// <para><b>Frame flow:</b> engine <c>SpriteAtlasAnimationSystem</c> (early parallel) advances clips; <c>SpriteAtlasBindingSystem</c> + <c>SpriteLocalizedAssetSystem</c> (late serial) resolve textures; <c>HudUiSystem</c> runs before <c>ui-document-frame</c>.</para>
/// <para><b>MSDF bootstrap:</b> sync <see cref="ModLoadContext.LoadBakedMsdfAtlas"/> in <see cref="LoadBuiltinFonts"/> (IdleGold pattern) — avoid <c>bold: true</c> on builtin UiSans without a Bold TTF.</para>
/// <para><b>World vs HUD:</b> gallery rows use world-space <c>BitmapText</c> + sprites; HUD is viewport retained UI — see <c>cyberland-world-screen-space</c>.</para>
/// <para><b>Locale contrast:</b> <c>--lang=de</c> for row D green blink vs E red/blue; <c>--lang=es</c> for Spanish HUD strings.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/spritegallery.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTable("sprite_gallery.json");
        LoadBuiltinFonts(context);
        PreloadSpriteAtlases(context);

        var hud = await SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.spritegallery/hud-ui",
            new HudUiSystem(host, hud));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap Sprite Gallery from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "Sprite Gallery scene spawn failed.");

        return ResolveHudRefs(context);
    }

    private static void LoadBuiltinFonts(ModLoadContext context)
    {
        // Builtin UiSans ships only a Regular TTF; bold:true misses Bold baked atlases (lookup uses Regular face).
        // Match sizePixels to a loaded Regular manifest (14 / 18) and avoid bold on HUD + scene labels.
        ReadOnlySpan<string> manifests =
        [
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18,
            BuiltinFonts.BakedAtlasManifestPath.MonoRegular14
        ];

        foreach (var path in manifests)
            context.LoadBakedMsdfAtlas(path);
    }

    private static void PreloadSpriteAtlases(ModLoadContext context)
    {
        context.LoadSpriteAtlas("Textures/Atlases/gallery.atlas.json");
        context.LoadSpriteAtlas("Textures/Atlases/ui_panel.atlas.json");
    }
}
