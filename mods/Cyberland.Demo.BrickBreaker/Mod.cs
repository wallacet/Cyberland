using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Breakout sample: chunk-parallel layout, grid brick hits (only ball + paddle use <c>TriggerSystem</c>), and cheap win via
/// <see cref="GameState.ActiveBricks"/>.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/>; then follow phase responsibilities in system registrations below.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/demo_brickbreaker.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("brick.json");
        KickoffBuiltinAtlasLoads(context);
        BrickBreakerHudGlyphWarmup.Warm(context);

        var host = context.Host;

        await SetupSceneAsync(context);

        context.RegisterSingleton("cyberland.demo.brick/input", new InputSystem(host));
        context.RegisterParallel("cyberland.demo.brick/layout", new ArenaLayoutSystem());
        context.RegisterSingleton("cyberland.demo.brick/round-start", new RoundStartSystem());
        context.RegisterParallel("cyberland.demo.brick/brick-reactivate", new ReactivateSystem());
        context.RegisterSingleton("cyberland.demo.brick/paddle-move", new PaddleMoveSystem());
        context.RegisterSingleton("cyberland.demo.brick/ball-launch", new BallLaunchSystem());
        context.RegisterSingleton("cyberland.demo.brick/ball-integrate", new BallIntegrateSystem());
        context.RegisterSingleton("cyberland.demo.brick/trigger-resolve", new TriggerResolveSystem());
        context.RegisterSingleton("cyberland.demo.brick/winlose", new WinLoseSystem());
        context.RegisterSingleton("cyberland.demo.brick/lights", new LightsFillSystem());

        context.RegisterParallel("cyberland.demo.brick/cell-sprites", new CellSpriteSyncSystem());
        context.RegisterParallel("cyberland.demo.brick/background-sprite", new BackgroundSpriteSyncSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/paddle-sprite", new PaddleSpriteSyncSystem());
        context.RegisterSingleton("cyberland.demo.brick/ball-sprite", new BallSpriteSyncSystem());
        context.RegisterSingleton("cyberland.demo.brick/title-ui-sprite", new TitleUiSpriteSyncSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/game-over-panel-sprite", new GameOverPanelSpriteSyncSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/game-over-bar-sprite", new GameOverBarSpriteSyncSystem(host));
        context.RegisterSerial("cyberland.demo.brick/life-sprites", new LifeSpriteSyncSystem(host));

        context.RegisterSingleton("cyberland.demo.brick/hud-title", new HudTitleTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/hud-hint-title", new HudHintTitleTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/hud-game-over", new HudGameOverTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/hud-hint-end", new HudHintEndTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/hud-playing-score", new HudPlayingScoreTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/hud-score-num", new HudScoreNumTextSystem(host));
        context.RegisterSingleton("cyberland.demo.brick/fps-hud", new FpsHudSystem(host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap BrickBreaker from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "BrickBreaker scene spawn failed.");

        BrickBreakerSceneWire.Apply(context.World);
    }

    private static void KickoffBuiltinAtlasLoads(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansBold23);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
    }
}
