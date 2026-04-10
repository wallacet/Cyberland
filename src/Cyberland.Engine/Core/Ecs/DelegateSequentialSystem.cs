namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Wraps a delegate as <see cref="ISystem"/> for lightweight registration (e.g. mod bootstrap that
/// does not warrant a dedicated type).
/// </summary>
public sealed class DelegateSequentialSystem : ISystem
{
    private readonly Action<World, float> _onUpdate;
    private readonly Action<World>? _onStart;

    /// <summary>Wraps lambdas as an <see cref="ISystem"/> for quick experiments or tiny mods.</summary>
    /// <param name="onUpdate">Required per-frame callback.</param>
    /// <param name="onStart">Optional one-time setup (see <see cref="ISystem.OnStart"/>).</param>
    public DelegateSequentialSystem(Action<World, float> onUpdate, Action<World>? onStart = null)
    {
        _onUpdate = onUpdate ?? throw new ArgumentNullException(nameof(onUpdate));
        _onStart = onStart;
    }

    /// <inheritdoc />
    public void OnStart(World world) => _onStart?.Invoke(world);

    /// <inheritdoc />
    public void OnUpdate(World world, float deltaSeconds) =>
        _onUpdate(world, deltaSeconds);
}
