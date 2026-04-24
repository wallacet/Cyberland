using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Pong;

public struct Control : IComponent
{
    public bool StartMatch;
    public bool PaddleUp;
    public bool PaddleDown;
}
