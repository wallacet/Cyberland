using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Scene;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class TriggerResolveSystem : ISystem, IFixedUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag, TriggerEvents>();


    private World _world = null!;
    private readonly Random _rng = new(424242);
    private EntityId _stateEntity;
    private EntityId _collectibleEntity;

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
        _stateEntity = world.QueryChunks(SystemQuerySpec.All<GameState>())
            .RequireSingleEntityWith<GameState>("game state");
        _collectibleEntity = world.QueryChunks(SystemQuerySpec.All<CollectibleTag, Transform>())
            .RequireSingleEntityWith<CollectibleTag>("collectible");
    }

    public void OnFixedUpdate(ChunkQueryAll query, float fixedDeltaSeconds)
    {
        _ = fixedDeltaSeconds;
        ref var state = ref _world.Get<GameState>(_stateEntity);
        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return;

        var w = _world;

        foreach (var chunk in query)
        {
            var triggerEventsCol = chunk.Column<TriggerEvents>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var triggerEvents = triggerEventsCol[i];
                if (triggerEvents.Events is null)
                    continue;

                foreach (var ev in triggerEvents.Events)
                {
                    var other = ev.Other;
                    if (w.Has<CollectibleTag>(other) && ev.Kind == TriggerEventKind.OnTriggerEnter)
                    {
                        state.Score += 10;
                        state.LocaleSpriteSeen = true;
                        ref var collectibleTransform = ref w.Get<Transform>(_collectibleEntity);
                        MouseChaseRoundLogic.RespawnCollectible(ref collectibleTransform, _rng);
                        continue;
                    }

                    if (w.Has<EnterZoneTag>(other) && ev.Kind == TriggerEventKind.OnTriggerEnter)
                    {
                        state.EnterZoneSeen = true;
                        continue;
                    }

                    if (w.Has<StayZoneTag>(other) && ev.Kind == TriggerEventKind.OnTriggerStay)
                    {
                        state.StayZoneSeen = true;
                        state.Score += 1;
                        continue;
                    }

                    if (w.Has<ExitZoneTag>(other) && ev.Kind == TriggerEventKind.OnTriggerExit)
                    {
                        state.ExitZoneSeen = true;
                        state.Health -= 15f;
                        continue;
                    }

                    if (w.Has<GateZoneTag>(other) && ev.Kind == TriggerEventKind.OnTriggerEnter && state.Score >= state.TargetScore)
                        state.Phase = RoundPhase.Won;
                }
            }
        }
    }
}
