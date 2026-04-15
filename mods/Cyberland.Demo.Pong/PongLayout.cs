namespace Cyberland.Demo.Pong;

/// <summary>
/// Screen-space layout for Pong HUD and chrome (pixel coordinates, origin bottom-left of framebuffer).
/// Keeps magic numbers out of systems so this demo reads like a small layout spec.
/// </summary>
internal static class PongLayout
{
    public const float HudMarginX = 36f;
    public const float ScoreBarBaseY = 80f;
    public const float ScoreBarHalfWidth = 10f;
    public const float ScoreBarMinHalfHeight = 2f;
    public const float ScoreColumnPlayerX = 18f;
    public const float ScoreNumOffsetFromLabel = 86f;
    public const float ScoreColumnCpuLabelOffset = 210f;
    public const float ScoreColumnCpuNumOffset = 88f;
    public const float TitleTextOffsetY = 48f;
    public const float GameOverTextOffsetY = 78f;
    public const float HintSpriteYTitle = 100f;
    public const float HintSpriteYGameOver = 130f;
    public const float TitleBarYPlaying = 28f;
    public const float TitleBarYMenu = 42f;
    public const float TitleBarWidthPlaying = 0.38f;
    public const float TitleBarWidthMenu = 0.45f;
    public const float TitleBarHalfHPlaying = 12f;
    public const float TitleBarHalfHMenu = 18f;
    public const float HintBarHalfH = 14f;
    public const float HintBarWidthFrac = 0.4f;
}
