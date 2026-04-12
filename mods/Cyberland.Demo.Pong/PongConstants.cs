namespace Cyberland.Demo.Pong;

/// <summary>Gameplay tuning. Simulation runs in <see cref="Cyberland.Engine.Core.Ecs.IFixedUpdate"/> using the engine
/// <see cref="Cyberland.Engine.Core.Tasks.SystemScheduler.FixedDeltaSeconds"/> each substep — speeds here are
/// <strong>pixels per second</strong> (or radians/sec for pulse), multiplied by that fixed dt, so motion does not depend on
/// display refresh or variable frame time.</summary>
public static class PongConstants
{
    public const int WinScore = 7;

    /// <summary>Target ball speed magnitude after normalization (px/s at 1:1 simulation time).</summary>
    public const float BallSpeed = 420f;

    public const float PaddleHalfH = 56f;
    public const float PaddleHalfW = 10f;
    public const float BallR = 8f;

    /// <summary>Player paddle vertical speed (px/s).</summary>
    public const float PlayerPaddleSpeed = 380f;

    /// <summary>CPU paddle pursuit speed (px/s).</summary>
    public const float CpuPaddleSpeed = 260f;

    /// <summary>Extra vertical velocity from paddle hits, before re-normalizing to <see cref="BallSpeed"/>.</summary>
    public const float PaddleEnglish = 180f;

    /// <summary>Neon title pulse phase speed (rad/s).</summary>
    public const float TitlePulseSpeed = 3f;

    /// <summary>Pause before the ball moves after a point (seconds of fixed time).</summary>
    public const float ServeDelaySeconds = 0.35f;
}
