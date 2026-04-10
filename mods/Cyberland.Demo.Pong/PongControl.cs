namespace Cyberland.Demo.Pong;

/// <summary>Written by <see cref="PongInputSystem"/>; consumed each tick by <see cref="PongSimulationSystem"/>.</summary>
public struct PongControl
{
    public bool StartMatch;
    public bool PaddleUp;
    public bool PaddleDown;
}
