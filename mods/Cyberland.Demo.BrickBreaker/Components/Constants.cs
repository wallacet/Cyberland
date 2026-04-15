namespace Cyberland.Demo.BrickBreaker;

public static class Constants
{
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
