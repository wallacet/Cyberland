using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>Per-unit selection and move-order state (formation slot target, independent of session).</summary>
public struct RtsUnitState : IComponent
{
    public bool Selected;
    public bool HasMoveOrder;
    public Vector2D<float> MoveTargetWorld;
}
