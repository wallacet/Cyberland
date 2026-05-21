using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Rts.Components;

/// <summary>
/// Smooth zoom targets for the RTS camera (<see cref="Camera2D.ViewportSizeWorld"/> lerps toward these each frame).
/// Width drives height to preserve 16:9.
/// </summary>
public struct CameraZoomState : IComponent
{
    public float TargetViewportWidth;
    public float TargetViewportHeight;
}
