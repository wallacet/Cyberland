using System.Diagnostics.CodeAnalysis;

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

        id = new ComponentId(_nextId++);
        _byType[type] = id;
        _byId[id] = type;
        _columnFactories[id] = static cap => new Column<T>(cap);
        return id;
    }

    public ColumnBase CreateColumn(ComponentId id, int capacity) => _columnFactories[id](capacity);

    [ExcludeFromCodeCoverage(Justification = "Unreachable unless uint.MaxValue component types are registered.")]
    private void EnsureComponentIdSpace()
    {
        if (_nextId == uint.MaxValue)
            throw new InvalidOperationException("Component type limit reached.");
    }
}
