namespace Cyberland.Demo.Snake;

/// <summary>Start/restart request from <see cref="SnakeInputSystem"/>; cleared by <see cref="SnakeTickSystem"/>.</summary>
public struct SnakeControl
{
    public bool StartGame;
}
