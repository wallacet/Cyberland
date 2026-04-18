namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Declares which component types must be present on entities for a scheduler-driven ECS system (Unity-style <c>All</c> filter).
/// The scheduler resolves this against the <see cref="World"/> and passes matching <see cref="ChunkQueryAll"/> views into phase callbacks.
/// </summary>
/// <remarks>
/// Use <see cref="Empty"/> for systems that only read singleton entities or host services and do not iterate chunk queries.
/// </remarks>
public readonly struct SystemQuerySpec : IEquatable<SystemQuerySpec>
{
    /// <summary>Distinct component struct types, sorted by full name for stable ordering.</summary>
    internal Type[] Types { get; }

    internal SystemQuerySpec(Type[] types) => Types = types;

    /// <summary>No chunk iteration; callbacks receive an empty <see cref="ChunkQueryAll"/>.</summary>
    public static SystemQuerySpec Empty => new(Array.Empty<Type>());

    /// <summary>Entities that have <typeparamref name="T"/>.</summary>
    public static SystemQuerySpec All<T>() where T : struct =>
        new(new[] { typeof(T) });

    /// <summary>Entities that have both <typeparamref name="T0"/> and <typeparamref name="T1"/>.</summary>
    public static SystemQuerySpec All<T0, T1>()
        where T0 : struct
        where T1 : struct =>
        new(SortUnique(typeof(T0), typeof(T1)));

    /// <summary>Entities that have <typeparamref name="T0"/>, <typeparamref name="T1"/>, and <typeparamref name="T2"/>.</summary>
    public static SystemQuerySpec All<T0, T1, T2>()
        where T0 : struct
        where T1 : struct
        where T2 : struct =>
        new(SortUnique(typeof(T0), typeof(T1), typeof(T2)));

    private static Type[] SortUnique(params Type[] types)
    {
        var unique = new HashSet<Type>();
        foreach (var t in types)
        {
            if (!unique.Add(t))
                throw new ArgumentException("Duplicate component types in query.");
        }

        var arr = new Type[unique.Count];
        unique.CopyTo(arr);
        Array.Sort(arr, (a, b) => string.CompareOrdinal(a.FullName, b.FullName));
        return arr;
    }

    /// <inheritdoc />
    public bool Equals(SystemQuerySpec other) => Types.AsSpan().SequenceEqual(other.Types.AsSpan());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SystemQuerySpec o && Equals(o);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hc = new HashCode();
        foreach (var t in Types)
            hc.Add(t);
        return hc.ToHashCode();
    }

    /// <summary>
    /// Column index for <typeparamref name="T"/> in <see cref="MultiComponentChunkView.Column{T}"/> for this spec
    /// (matches sorted runtime component ids).
    /// </summary>
    public int GetColumnIndex<T>(World world) where T : struct =>
        world.GetQueryColumnIndex<T>(this);

    internal ComponentId[] ResolveSortedComponentIds(ComponentRegistry registry)
    {
        if (Types.Length == 0)
            return Array.Empty<ComponentId>();

        var ids = new ComponentId[Types.Length];
        for (var i = 0; i < Types.Length; i++)
            ids[i] = registry.GetOrRegister(Types[i]);

        Array.Sort(ids);
        return DedupeSortedIds(ids);
    }

    private static ComponentId[] DedupeSortedIds(ComponentId[] sorted)
    {
        if (sorted.Length <= 1)
            return sorted;

        var w = 0;
        for (var r = 1; r < sorted.Length; r++)
        {
            if (sorted[r] != sorted[w])
                sorted[++w] = sorted[r];
        }

        var len = w + 1;
        if (len == sorted.Length)
            return sorted;

        var result = new ComponentId[len];
        Array.Copy(sorted, result, len);
        return result;
    }
}
