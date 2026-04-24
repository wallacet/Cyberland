using System.Collections.Concurrent;
using System.Reflection;

namespace Cyberland.Engine.Core.Ecs;

internal static class RequiresComponentEnforcer
{
    private static readonly ConcurrentDictionary<Type, Type[]> RequiredTypesCache = new();
    private static readonly ConcurrentDictionary<Type, Action<World, EntityId>> EnsureDefaultCache = new();

    private static readonly MethodInfo RefGetOrAddEntityOnly;

    static RequiresComponentEnforcer()
    {
        RefGetOrAddEntityOnly = typeof(World).GetMethod(
            nameof(World.RefGetOrAdd),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(EntityId) },
            modifiers: null)!;
    }

    internal static void EnsureDependencies<TDecorated>(World world, EntityId entity) where TDecorated : struct, IComponent
    {
        foreach (var req in GetRequiredTypes(typeof(TDecorated)))
            EnsureDefault(world, entity, req);
    }

    private static Type[] GetRequiredTypes(Type decorated)
    {
        return RequiredTypesCache.GetOrAdd(decorated, static t =>
        {
            var def = typeof(RequiresComponentAttribute<>);
            var list = new List<Type>();
            foreach (var attr in t.GetCustomAttributes(inherit: false))
            {
                var at = attr.GetType();
                if (!at.IsGenericType || at.GetGenericTypeDefinition() != def)
                    continue;

                var req = at.GetGenericArguments()[0];
                if (list.Contains(req))
                    continue;

                list.Add(req);
            }

            return list.ToArray();
        });
    }

    private static void EnsureDefault(World world, EntityId entity, Type componentType)
    {
        var action = EnsureDefaultCache.GetOrAdd(componentType, BuildEnsureDefault);
        action(world, entity);
    }

    private static Action<World, EntityId> BuildEnsureDefault(Type componentType)
    {
        var closed = RefGetOrAddEntityOnly.MakeGenericMethod(componentType);
        return (world, entity) => _ = closed.Invoke(world, new object[] { entity });
    }
}
