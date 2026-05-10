using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// ECS singleton state for one match.
/// </summary>
/// <remarks>Lives on the entity marked by <see cref="SessionTag"/> after <see cref="SceneSetup"/> runs.</remarks>
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

    /// <summary>Set by <see cref="RoundStartSystem"/>; consumed by <see cref="ReactivateSystem"/> in the same fixed pass.</summary>
    public bool PendingReactivation;

    /// <summary>Active bricks remaining; reset when blocks reactivate, decremented on breaks for cheap win checks.</summary>
    public int ActiveBricks;
}
