namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Wraps delegates as <see cref="ISystem"/> for lightweight registration (e.g. mod bootstrap that
/// does not warrant a dedicated type).
/// </summary>
public sealed class DelegateSequentialSystem : ISystem, IEarlyUpdate, IFixedUpdate, ILateUpdate
{
    private readonly Action<World, float>? _onEarlyUpdate;
    private readonly Action<World, float>? _onFixedUpdate;
    private readonly Action<World, float>? _onLateUpdate;
    private readonly Action<World>? _onStart;

    /// <summary>Wraps lambdas as an <see cref="ISystem"/> for quick experiments or tiny mods.</summary>
    /// <param name="onEarlyUpdate">Optional per-frame callback before fixed simulation.</param>
    /// <param name="onFixedUpdate">Optional fixed timestep callback.</param>
    /// <param name="onLateUpdate">Optional per-frame callback after fixed simulation.</param>
    /// <param name="onStart">Optional one-time setup (see <see cref="ISystem.OnStart"/>).</param>
    public DelegateSequentialSystem(
        Action<World, float>? onEarlyUpdate = null,
        Action<World, float>? onFixedUpdate = null,
        Action<World, float>? onLateUpdate = null,
        Action<World>? onStart = null)
    {
        if (onEarlyUpdate is null && onFixedUpdate is null && onLateUpdate is null)
            throw new ArgumentException("At least one of early, fixed, or late update delegates must be non-null.");

        _onEarlyUpdate = onEarlyUpdate;
        _onFixedUpdate = onFixedUpdate;
        _onLateUpdate = onLateUpdate;
        _onStart = onStart;
    }

    /// <inheritdoc />
    public void OnStart(World world) => _onStart?.Invoke(world);

    /// <inheritdoc />
    public void OnEarlyUpdate(World world, float deltaSeconds) =>
        _onEarlyUpdate?.Invoke(world, deltaSeconds);

    /// <inheritdoc />
    public void OnFixedUpdate(World world, float fixedDeltaSeconds) =>
        _onFixedUpdate?.Invoke(world, fixedDeltaSeconds);

    /// <inheritdoc />
    public void OnLateUpdate(World world, float deltaSeconds) =>
        _onLateUpdate?.Invoke(world, deltaSeconds);
}
