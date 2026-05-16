using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo.WhackAMole;

/// <summary>
/// Whack-a-Mole sample: one target square at a time, score-on-click, one-minute countdown after first hit.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for entities, then <see cref="Systems.WhackAMoleGameSystem"/> for the full game loop on a singleton row.</para>
/// <para><b>Architecture:</b> intentionally minimal — one <see cref="Cyberland.Engine.Core.Ecs.ISingletonSystem"/> implementation drives input, spawning, timer, and HUD string updates so newcomers see ECS scheduling without a long registration list.</para>
/// <para><b>MSDF:</b> synchronous <see cref="SeedHudMsdfAtlases"/> matches the IdleGold pattern (guaranteed glyphs before first frame).</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        WhackAMoleInputSetup.RegisterDefaultBindings(context);
        // Use synchronous LoadBakedMsdfAtlas here — awaiting LoadBakedMsdfAtlasAsync from OnLoadAsync deadlocks because
        // the task completes only after the render loop drains uploads, which does not run until mod load returns.
        SeedHudMsdfAtlases(context);

        var scene = await SceneSetup.SetupSceneAsync(context).ConfigureAwait(false);
        context.RegisterSingleton("cyberland.demo.whackamole/game", new Systems.WhackAMoleGameSystem(context.Host, scene));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    /// <summary>
    /// Seeds baked MSDF pages for HUD text sizes (16 / 20 / 24 px) before the first frame.
    /// </summary>
    private static void SeedHudMsdfAtlases(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16);
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20);
        _ = context.LoadBakedMsdfAtlas(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular24);
    }
}
