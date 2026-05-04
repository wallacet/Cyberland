using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Which life pip (0..2) a UI sprite represents; used by <see cref="LifeSpriteSyncSystem"/>.</summary>
public struct LifePipSlot : IComponent
{
    public byte Index;
}
