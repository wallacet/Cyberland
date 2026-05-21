using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

/// <summary>Tag-based entity resolution after <c>Scenes/pong.json</c> spawn.</summary>
internal static class SceneWire
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
}
