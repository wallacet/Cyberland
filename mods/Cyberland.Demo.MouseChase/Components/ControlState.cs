using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Components;

public struct ControlState : IComponent
{
    public Vector2D<float> MouseWorld;
    public float ZoomDelta;
    public bool PrimaryPressed;
    public bool RestartPressed;
}
