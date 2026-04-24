using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>ECS component used by <see cref="VelocityDampSystem"/> in this mod.</summary>
public struct Velocity : IComponent
{
    public float X;
    public float Y;
}
