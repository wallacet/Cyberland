namespace Cyberland.Demo.BrickBreaker;

public static class Constants
{
    /// <summary>
    /// Virtual canvas size in world pixels for this mod's <see cref="Cyberland.Engine.Scene.Camera2D"/> and all arena/HUD layout.
    /// </summary>
    /// <remarks>
    /// Must stay in sync with the camera entity created in <c>Mod.OnLoad</c>. Do not use
    /// <see cref="Cyberland.Engine.Rendering.IRenderer.ActiveCameraViewportSize"/> from parallel <strong>early</strong> systems: the
    /// renderer has not ingested camera submissions yet that frame, so that property can still reflect the
    /// swapchain size and would mis-size the arena relative to the fixed camera viewport.
    /// </remarks>
    public const int CanvasWidth = 1280;

    /// <summary>Height of the virtual canvas; see <see cref="CanvasWidth"/>.</summary>
    public const int CanvasHeight = 720;

    public const int Cols = 10;
    public const int Rows = 6;
    public const int BrickPoints = 10;
    public const int StartingLives = 3;
    public const float BallSpeed = 380f;
    public const float BallR = 8f;
    /// <summary>Horizontal paddle movement in world units per second.</summary>
    public const float PaddleMoveSpeed = 420f;
    /// <summary>Extra horizontal velocity from hitting off-center on the paddle.</summary>
    public const float PaddleEnglish = 140f;
    /// <summary>Pixels below <see cref="GameState.ArenaMinY"/> before the ball counts as lost.</summary>
    public const float BallFallSafetyBand = 80f;
}
