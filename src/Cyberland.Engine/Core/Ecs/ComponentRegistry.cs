using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Maps component CLR types to <see cref="ComponentId"/> for one <see cref="World"/>.
/// </summary>
internal sealed class ComponentRegistry
{
    private readonly Dictionary<Type, ComponentId> _byType = new();
    private readonly Dictionary<ComponentId, Type> _byId = new();
    private readonly Dictionary<ComponentId, Func<int, ColumnBase>> _columnFactories = new();
    private uint _nextId;

    public ComponentId GetOrRegister<T>() where T : struct
    {
        var type = typeof(T);
        if (_byType.TryGetValue(type, out var id))
            return id;

        EnsureComponentIdSpace();

        id = _nextId++;
        _byType[type] = id;
        _byId[id] = type;
        _columnFactories[id] = static cap => new Column<T>(cap);
        return id;
    }

    /// <summary>Resolves a struct component type to its id (same as <see cref="GetOrRegister{T}"/>).</summary>
    public ComponentId GetOrRegister(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsValueType)
            throw new ArgumentException("Component type must be a struct.", nameof(type));
        if (_byType.TryGetValue(type, out var id))
            return id;

        var generic = typeof(ComponentRegistry)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == nameof(GetOrRegister) && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

        return (ComponentId)generic.MakeGenericMethod(type).Invoke(this, null)!;
    }

    public ColumnBase CreateColumn(ComponentId id, int capacity) => _columnFactories[id](capacity);

    [ExcludeFromCodeCoverage(Justification = "Unreachable unless uint.MaxValue component types are registered.")]
    private void EnsureComponentIdSpace()
    {
        if (_nextId == uint.MaxValue)
            throw new InvalidOperationException("Component type limit reached.");
    }
}
