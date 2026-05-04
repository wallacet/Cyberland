using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo;

/// <summary>
/// Per-axis velocity in world units per second. <see cref="IntegrateSystem"/> consumes it each fixed tick;
/// <see cref="InputSystem"/> writes it from axes; <see cref="VelocityDampSystem"/> optionally damps it for smoother stops.
/// </summary>
public struct Velocity : IComponent
{
    public float X;
    public float Y;
}
