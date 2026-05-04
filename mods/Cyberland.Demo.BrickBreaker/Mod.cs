using Cyberland.Engine;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Breakout sample: many entities, chunk-parallel layout and win detection, and trigger-based hits after the engine
/// <c>TriggerSystem</c> runs in the fixed chain.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> registration order mirrors the HDR demo—see <see cref="SceneSetup"/> for the static scene recipe,
/// then follow phase responsibilities below.</para>
/// <para><b>Frame flow (simplified):</b>
/// <see cref="Systems.InputSystem"/> (early singleton) →
/// <see cref="Systems.ArenaLayoutSystem"/> (early, parallel) →
/// <see cref="Systems.RoundStartSystem"/> → <see cref="Systems.ReactivateSystem"/> →
/// <see cref="Systems.PaddleMoveSystem"/> / <see cref="Systems.BallLaunchSystem"/> / <see cref="Systems.BallIntegrateSystem"/> / <see cref="Systems.TriggerResolveSystem"/> →
/// <see cref="Systems.WinLoseSystem"/> (fixed, parallel) →
/// <see cref="Systems.LightsFillSystem"/> (late) →
/// multiple query-driven <c>brick/*</c> late sprite and HUD systems (see individual types).</para>
/// <para><b>Registration order matters</b> for <see cref="GameState.PendingReactivation"/> and win detection: round start and
/// block reactivation run before <see cref="Systems.WinLoseSystem"/> in the same fixed pass so a new round is not misread as a clear board.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("brick.json");

        var host = context.Host;

        await SceneSetup.SetupSceneAsync(context);

        context.RegisterSingleton("cyberland.demo.brick/input", new InputSystem(host));
        context.RegisterParallel("cyberland.demo.brick/layout", new ArenaLayoutSystem());
        context.RegisterSingleton("cyberland.demo.brick/round-start", new RoundStartSystem());
        context.RegisterParallel("cyberland.demo.brick/brick-reactivate", new ReactivateSystem());
        context.RegisterSingleton("cyberland.demo.brick/paddle-move", new PaddleMoveSystem());
        context.RegisterSingleton("cyberland.demo.brick/ball-launch", new BallLaunchSystem());
        context.RegisterSingleton("cyberland.demo.brick/ball-integrate", new BallIntegrateSystem());
        context.RegisterSingleton("cyberland.demo.brick/trigger-resolve", new TriggerResolveSystem());
        context.RegisterParallel("cyberland.demo.brick/winlose", new WinLoseSystem());
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
}
