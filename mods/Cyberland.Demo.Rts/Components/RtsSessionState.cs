using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>Singleton row: entity wiring and per-session RTS state (selection, move order).</summary>
public struct RtsSessionState : IComponent
{
    public EntityId CameraEntity;
    public EntityId UnitEntity;
    public EntityId SelectionBar0;
    public EntityId SelectionBar1;
    public EntityId SelectionBar2;
    public EntityId SelectionBar3;
    public bool UnitSelected;
    public bool HasMoveTarget;
    public Vector2D<float> MoveTargetWorld;
}
