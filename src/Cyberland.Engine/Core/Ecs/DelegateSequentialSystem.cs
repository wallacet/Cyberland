namespace Cyberland.Engine.Core.Ecs;

/// <summary>
/// Wraps a delegate as <see cref="ISystem"/> for lightweight registration (e.g. mod bootstrap that
/// does not warrant a dedicated type).
/// </summary>
public sealed class DelegateSequentialSystem : ISystem
{
    private readonly Action<World, float> _onUpdate;
    private readonly Action<World>? _onStart;

    public DelegateSequentialSystem(Action<World, float> onUpdate, Action<World>? onStart = null)
    {
        _onUpdate = onUpdate ?? throw new ArgumentNullException(nameof(onUpdate));
        _onStart = onStart;
    }

    public void OnStart(World world) => _onStart?.Invoke(world);

    public void OnUpdate(World world, float deltaSeconds) =>
        _onUpdate(world, deltaSeconds);
}
