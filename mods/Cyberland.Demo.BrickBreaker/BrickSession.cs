using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Heap-allocated brick grid and playfield state. Kept outside ECS chunks (see plan); systems remain split.
/// </summary>
public sealed class BrickSession
{
    public BrickPhase Phase = BrickPhase.Title;
    public float ArenaMinX;
    public float ArenaMaxX;
    public float ArenaMinY;
    public float ArenaMaxY;
    public float PaddleY;
    public float PaddleHalfW = 72f;
    public float PaddleHalfH = 10f;
    public float PaddleCenterX;
    public Vector2D<float> BallPos;
    public Vector2D<float> BallVel;
    public bool BallDocked = true;
    public readonly bool[,] Bricks = new bool[BrickConstants.Cols, BrickConstants.Rows];
    public int Lives = BrickConstants.StartingLives;
    public int Score;
    public float BrickW;
    public float BrickH;
    public float BrickOriginX;
    public float BrickTopY;
}
