using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>Singleton row: camera/selection-bar wiring and marquee drag state.</summary>
public struct RtsSessionState : IComponent
{
    public EntityId CameraEntity;
    public EntityId SelectionBar0;
    public EntityId SelectionBar1;
    public EntityId SelectionBar2;
    public EntityId SelectionBar3;
    public bool BoxDragActive;
    public Vector2D<float> BoxDragStartWorld;
    public Vector2D<float> BoxDragEndWorld;
}
