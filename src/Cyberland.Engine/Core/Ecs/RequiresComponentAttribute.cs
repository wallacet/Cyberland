namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Declares that adding the decorated ECS component struct to an entity must also ensure another component type exists.
/// </summary>
/// <typeparam name="TRequired">Peer component struct type to ensure before the decorated component is added.</typeparam>
/// <remarks>
/// <para>
/// When code calls <see cref="ComponentStore{T}.GetOrAdd(EntityId)"/> (or the overload with an initial value), the engine
/// resolves required types first via <see cref="World"/> so dependent components can rely on peers being present.
/// </para>
/// <para>
/// Apply multiple attributes to list several requirements. Avoid dependency cycles between component types.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresComponentAttribute<TRequired> : Attribute
    where TRequired : struct, IComponent
{
}
