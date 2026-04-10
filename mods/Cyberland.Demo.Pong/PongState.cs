using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>Simulation state for the pong session entity.</summary>
public struct PongState
{
    public PongPhase Phase;
    public float Pulse;
    public int PlayerPoints;
    public int CpuPoints;
    public Vector2D<float> BallPos;
    public Vector2D<float> BallVel;
    public float LeftPaddleY;
    public float RightPaddleY;
    public float ArenaMinX;
    public float ArenaMaxX;
    public float ArenaMinY;
    public float ArenaMaxY;
    public float ServeDelay;
}
