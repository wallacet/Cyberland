using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Tag-based entity resolution after <c>Scenes/demo_pong.json</c> spawn.</summary>
internal static class PongSceneWire
{
    public static VisualIds ResolveVisuals(World world) => new(
        world.RequireSingleEntityWith<BackgroundSpriteTag>("Pong background sprite"),
        world.RequireSingleEntityWith<TitleBarSpriteTag>("Pong title bar sprite"),
        world.RequireSingleEntityWith<HintBarSpriteTag>("Pong hint bar sprite"),
        world.RequireSingleEntityWith<ScorePlayerSpriteTag>("Pong score player sprite"),
        world.RequireSingleEntityWith<ScoreCpuSpriteTag>("Pong score CPU sprite"),
        world.RequireSingleEntityWith<LeftPaddleSpriteTag>("Pong left paddle sprite"),
        world.RequireSingleEntityWith<RightPaddleSpriteTag>("Pong right paddle sprite"),
        world.RequireSingleEntityWith<BallSpriteTag>("Pong ball sprite"));

    public static HudTextIds ResolveHudTexts(World world) => new(
        world.RequireSingleEntityWith<HudTitleTextTag>("Pong HUD title"),
        world.RequireSingleEntityWith<HudGameOverTextTag>("Pong HUD game over"),
        world.RequireSingleEntityWith<HudHintTextTag>("Pong HUD hint"),
        world.RequireSingleEntityWith<HudScoreYouTextTag>("Pong HUD score you"),
        world.RequireSingleEntityWith<HudScorePlayerNumTextTag>("Pong HUD player score"),
        world.RequireSingleEntityWith<HudScoreCpuLabelTextTag>("Pong HUD CPU label"),
        world.RequireSingleEntityWith<HudScoreCpuNumTextTag>("Pong HUD CPU score"),
        world.RequireSingleEntityWith<HudFpsTextTag>("Pong HUD FPS"));
}
