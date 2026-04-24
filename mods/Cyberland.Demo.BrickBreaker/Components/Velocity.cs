using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Velocity component for BrickBreaker gameplay entities.
/// </summary>
public struct Velocity : IComponent
{
    public Vector2D<float> Value;
}
