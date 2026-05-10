using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>
/// Win when <see cref="GameState.ActiveBricks"/> reaches zero (maintained by <see cref="TriggerResolveSystem"/> / <see cref="ReactivateSystem"/>).
/// </summary>
public sealed class WinLoseSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionTag, GameState>();

    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        _ = sessionRow;
    }

    public void OnSingletonFixedUpdate(in SingletonEntity sessionRow, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var game = ref sessionRow.Get<GameState>();
        if (game.Phase != Phase.Playing)
            return;
        if (game.ActiveBricks > 0)
            return;
        game.Phase = Phase.Won;
    }
}
