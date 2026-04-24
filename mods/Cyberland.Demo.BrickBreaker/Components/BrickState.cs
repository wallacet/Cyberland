using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Runtime state for one brick cell entity.
/// </summary>
public struct BrickState : IComponent
{
    public bool Active;
}
