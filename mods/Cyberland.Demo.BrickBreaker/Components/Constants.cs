using Cyberland.Engine;

namespace Cyberland.Demo.BrickBreaker;

public static class Constants
{
    /// <summary>Sliding window (seconds) for the FPS HUD moving average. Tunable for debugging.</summary>
    public const float FpsAverageWindowSeconds = FpsMovingAverage.DefaultWindowSeconds;

    /// <summary>Viewport FPS row before the moving average is ready (uses U+2014 em dash; keep in sync with glyph warmup).</summary>
    public const string FpsHudAwaitingLabel = "FPS \u2014";

    /// <summary>
    /// Design-time virtual canvas (world pixels) for the mod’s <see cref="Cyberland.Engine.Scene.Camera2D"/> and simulation
    /// layout (<see cref="GameState"/> arena metrics, <see cref="ArenaLayoutSystem"/>). Matches HDR’s fixed virtual canvas pattern.
    /// </summary>
    /// <remarks>
    /// <para>Simulation phases should treat this as the playfield extent once the active camera is configured—typically aligned with
    /// <see cref="Cyberland.Engine.Hosting.ModLayoutViewport.VirtualSizeForSimulation"/> after startup.</para>
    /// <para>Viewport-space HUD (<see cref="Cyberland.Engine.Rendering.Text.BitmapText"/> in <c>ViewportSpace</c>) belongs in
    /// <strong>late</strong> update and should use <see cref="Cyberland.Engine.Hosting.ModLayoutViewport.VirtualSizeForPresentation"/>
    /// so letterboxing and DPI match the HDR demo.</para>
    /// <para>Do not use <see cref="Cyberland.Engine.Rendering.IRenderer.ActiveCameraViewportSize"/> from parallel <strong>early</strong>
    /// systems: the renderer may not have applied camera submissions yet.</para>
    /// </remarks>
    public const int CanvasWidth = 1280;

    /// <summary>Height of the virtual canvas; see <see cref="CanvasWidth"/>.</summary>
    public const int CanvasHeight = 720;

    public const int Cols = 10;
    public const int Rows = 6;
    public const int PointsPerBlock = 10;
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
