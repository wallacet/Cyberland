namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Marker for ECS component structs stored on <see cref="World"/> entities. Carries no runtime contract;
/// it exists so component types are easy to spot in source and query APIs can require component-shaped types.
/// </summary>
public interface IComponent
{
}
