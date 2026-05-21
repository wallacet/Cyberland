using Cyberland.Engine.Core.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Demo.Rts.Components;

/// <summary>Singleton row: camera/selection-bar wiring, marquee drag, control-group tap tracking, camera focus.</summary>
public struct RtsSessionState : IComponent
{
    /// <summary>No active recalled group (<see cref="RtsControlGroups"/> index 0–9).</summary>
    public const byte NoActiveGroup = 255;

    public EntityId CameraEntity;
    public EntityId SelectionBar0;
    public EntityId SelectionBar1;
    public EntityId SelectionBar2;
    public EntityId SelectionBar3;
    public bool BoxDragActive;
    public Vector2D<float> BoxDragStartWorld;
    public Vector2D<float> BoxDragEndWorld;

    public bool PendingCameraFocus;
    public Vector2D<float> PendingCameraFocusWorld;

    public byte ActiveGroupIndex;
    public byte LastGroupKeyIndex;
    public float LastGroupKeyTime;
    public float GroupKeyClock;
}
