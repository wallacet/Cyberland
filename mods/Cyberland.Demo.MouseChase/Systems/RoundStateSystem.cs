using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>
/// Timer, health, and tutorial→playing transition for the lone <see cref="GameState"/> row.
/// </summary>
public sealed class RoundStateSystem : ISingletonSystem, ISingletonFixedUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();

    /// <inheritdoc />
    public void OnSingletonFixedUpdate(in SingletonEntity singleton, float fixedDeltaSeconds)
    {
        ref var state = ref singleton.Get<GameState>();
        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;

        state.TimerSeconds -= fixedDeltaSeconds;
        if (state.TimerSeconds <= 0f || state.Health <= 0f)
        {
            state.Phase = RoundPhase.Lost;
            return;
        }

        if (state.Phase == RoundPhase.Tutorial
            && state.Score >= state.TargetScore
            && state.EnterZoneSeen
            && state.StayZoneSeen
            && state.ExitZoneSeen)
            state.Phase = RoundPhase.Playing;
    }
}
