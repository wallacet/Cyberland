using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Grid coordinate for one brick entity in the session brick map.
/// </summary>
public struct Cell : IComponent
{
    public int X;
    public int Y;
}
