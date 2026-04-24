using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Shape data for the BrickBreaker paddle body.
/// </summary>
public struct PaddleBody : IComponent
{
    public float HalfWidth;
    public float HalfHeight;
}
