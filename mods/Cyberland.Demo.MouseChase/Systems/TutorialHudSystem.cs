using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

public sealed class TutorialHudSystem : ISystem, ILateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.Empty;


    private World _world = null!;
    private readonly LocalizationManager _strings;
    private readonly EntityId _stateEntity;
    private readonly EntityId _titleTextEntity;
    private readonly EntityId _detailTextEntity;
    private readonly EntityId _statusTextEntity;
    public TutorialHudSystem(LocalizationManager strings, EntityId stateEntity, EntityId titleTextEntity, EntityId detailTextEntity,
        EntityId statusTextEntity)
    {
        _strings = strings;
        _stateEntity = stateEntity;
        _titleTextEntity = titleTextEntity;
        _detailTextEntity = detailTextEntity;
        _statusTextEntity = statusTextEntity;
    }

    public void OnStart(World world, ChunkQueryAll query)
    {
        _world = world;
        _ = query;
    }

    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = query;
        _ = deltaSeconds;
        ref readonly var state = ref _world.Get<GameState>(_stateEntity);

        UpdateHudText(_titleTextEntity, HeaderText(state), new Vector2D<float>(40f, 36f), 24f);
        UpdateHudText(_detailTextEntity, DetailText(state), new Vector2D<float>(40f, 74f), 18f);
        UpdateHudText(_statusTextEntity, StatusText(state), new Vector2D<float>(40f, 108f), 18f);
    }

    private void UpdateHudText(EntityId entity, string text, Vector2D<float> viewportPos, float size)
    {
        ref var transform = ref _world.Get<Transform>(entity);
        transform.WorldPosition = viewportPos;
        transform.LocalPosition = viewportPos;

        ref var bt = ref _world.Get<BitmapText>(entity);
        bt.Visible = true;
        bt.Content = text;
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.Style = bt.Style with { SizePixels = size };
    }

    private string HeaderText(in GameState state) =>
        state.Phase switch
        {
            RoundPhase.Won => _strings.Get("mousechase.round.won"),
            RoundPhase.Lost => _strings.Get("mousechase.round.lost"),
            _ => _strings.Get("mousechase.round.title")
        };

    private string DetailText(in GameState state)
    {
        if (state.Phase is RoundPhase.Won or RoundPhase.Lost)
            return _strings.Get("mousechase.round.restart");

        if (!state.EnterZoneSeen)
            return _strings.Get("mousechase.tutorial.enter");
        if (!state.StayZoneSeen)
            return _strings.Get("mousechase.tutorial.stay");
        if (!state.ExitZoneSeen)
            return _strings.Get("mousechase.tutorial.exit");
        if (!state.LocaleSpriteSeen)
            return _strings.Get("mousechase.tutorial.locale");

        return _strings.Get("mousechase.tutorial.complete");
    }

    private static string StatusText(in GameState state) =>
        $"Score {state.Score}/{state.TargetScore}  Health {state.Health:0}  Time {state.TimerSeconds:0.0}s";
}
