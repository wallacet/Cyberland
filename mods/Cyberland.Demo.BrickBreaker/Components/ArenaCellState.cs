using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Runtime hit/active state for one arena grid cell entity.</summary>
public struct ArenaCellState : IComponent
{
    public bool Active;
}
