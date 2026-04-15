using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

public struct State
{
    public Phase Phase;
    public float Pulse;
    public int PlayerPoints;
    public int CpuPoints;
    public Vector2D<float> BallPos;
    public Vector2D<float> BallVel;
    public float LeftPaddleY;
    public float RightPaddleY;
    public float LeftPaddleVelY;
    public float RightPaddleVelY;
    public float ArenaMinX;
    public float ArenaMaxX;
    public float ArenaMinY;
    public float ArenaMaxY;
    public float ServeDelay;
}
