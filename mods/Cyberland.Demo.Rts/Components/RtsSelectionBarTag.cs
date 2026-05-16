using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.Rts.Components;

/// <summary>One of four unit selection frame bars; <see cref="Index"/> is 0–3.</summary>
public struct RtsSelectionBarTag : IComponent
{
    public byte Index;
}
