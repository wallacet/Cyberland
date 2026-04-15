namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Wraps delegates as <see cref="ISystem"/> for lightweight registration (e.g. mod bootstrap that
/// does not warrant a dedicated type). Set <see cref="QuerySpec"/> via the overload that takes <see cref="SystemQuerySpec"/> when delegates iterate chunks.
/// </summary>
public sealed class DelegateSequentialSystem : ISystem, IEarlyUpdate, IFixedUpdate, ILateUpdate
{
    private readonly SystemQuerySpec _querySpec;
    private readonly Action<World, ChunkQueryAll, float>? _onEarlyUpdate;
    private readonly Action<World, ChunkQueryAll, float>? _onFixedUpdate;
    private readonly Action<World, ChunkQueryAll, float>? _onLateUpdate;
    private readonly Action<World, ChunkQueryAll>? _onStart;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => _querySpec;

    /// <summary>Delegates only; uses <see cref="SystemQuerySpec.Empty"/>.</summary>
    public DelegateSequentialSystem(
        Action<World, ChunkQueryAll, float>? onEarlyUpdate = null,
        Action<World, ChunkQueryAll, float>? onFixedUpdate = null,
        Action<World, ChunkQueryAll, float>? onLateUpdate = null,
        Action<World, ChunkQueryAll>? onStart = null)
        : this(SystemQuerySpec.Empty, onEarlyUpdate, onFixedUpdate, onLateUpdate, onStart)
    {
    }

    /// <summary>Wraps lambdas as an <see cref="ISystem"/> with an explicit chunk query.</summary>
    /// <param name="querySpec">Chunk query for phase callbacks.</param>
    /// <param name="onEarlyUpdate">Optional per-frame callback before fixed simulation.</param>
    /// <param name="onFixedUpdate">Optional fixed timestep callback.</param>
    /// <param name="onLateUpdate">Optional per-frame callback after fixed simulation.</param>
    /// <param name="onStart">Optional one-time setup (see <see cref="ISystem.OnStart"/>).</param>
    public DelegateSequentialSystem(
        SystemQuerySpec querySpec,
        Action<World, ChunkQueryAll, float>? onEarlyUpdate = null,
        Action<World, ChunkQueryAll, float>? onFixedUpdate = null,
        Action<World, ChunkQueryAll, float>? onLateUpdate = null,
        Action<World, ChunkQueryAll>? onStart = null)
    {
        _querySpec = querySpec;
        if (onEarlyUpdate is null && onFixedUpdate is null && onLateUpdate is null)
            throw new ArgumentException("At least one of early, fixed, or late update delegates must be non-null.");

        _onEarlyUpdate = onEarlyUpdate;
        _onFixedUpdate = onFixedUpdate;
        _onLateUpdate = onLateUpdate;
        _onStart = onStart;
    }

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query) => _onStart?.Invoke(world, query);

    /// <inheritdoc />
    public void OnEarlyUpdate(World world, ChunkQueryAll query, float deltaSeconds) =>
        _onEarlyUpdate?.Invoke(world, query, deltaSeconds);

    /// <inheritdoc />
    public void OnFixedUpdate(World world, ChunkQueryAll query, float fixedDeltaSeconds) =>
        _onFixedUpdate?.Invoke(world, query, fixedDeltaSeconds);

    /// <inheritdoc />
    public void OnLateUpdate(World world, ChunkQueryAll query, float deltaSeconds) =>
        _onLateUpdate?.Invoke(world, query, deltaSeconds);
}
