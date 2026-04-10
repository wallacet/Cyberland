namespace Cyberland.Demo.BrickBreaker;

/// <summary>Consumed each frame by <see cref="BrickSimulationSystem"/>.</summary>
public struct BrickControl
{
    public bool StartRound;
    public bool MoveLeft;
    public bool MoveRight;
    public bool LaunchBall;
}
