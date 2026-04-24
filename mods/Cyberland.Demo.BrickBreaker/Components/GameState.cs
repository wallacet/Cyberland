using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// ECS singleton state for one BrickBreaker match.
/// </summary>
public struct GameState : IComponent
{
    public Phase Phase;
    public int Lives;
    public int Score;
    public bool BallDocked;
    public float ArenaMinX;
    public float ArenaMaxX;
    public float ArenaMinY;
    public float ArenaMaxY;
    public float PaddleY;
    public float BrickW;
    public float BrickH;
    public float BrickOriginX;
    public float BrickTopY;
    public bool LayoutInitialized;
    public int LayoutWidth;
    public int LayoutHeight;
}
