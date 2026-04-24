using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Input intent consumed by BrickBreaker fixed-update gameplay systems.</summary>
public struct Control : IComponent
{
    public bool StartRound;
    public bool MoveLeft;
    public bool MoveRight;
    public bool LaunchBall;
}
