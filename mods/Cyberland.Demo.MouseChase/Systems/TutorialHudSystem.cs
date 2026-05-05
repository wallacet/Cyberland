using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>
/// Tutorial copy lines driven by <see cref="GameState"/>; text entities are tagged rows resolved once at startup.
/// </summary>
/// <remarks>
/// <c>[RunBefore("cyberland.engine/text-render")]</c> documents intent: <see cref="BitmapText"/> must be authored before
/// <see cref="TextRenderSystem"/> builds glyphs that frame. In <see cref="GameApplication"/>, mods load before the engine
/// registers text-render, so ordering is usually safe without attributes; the constraint stays as explicit scheduler documentation.
/// </remarks>
[RunBefore("cyberland.engine/text-render")]
public sealed class TutorialHudSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();

    private readonly LocalizationManager _strings;
    private EntityId _titleEntity;
    private EntityId _detailEntity;
    private EntityId _statusEntity;

    /// <summary>Uses merged locale tables from <see cref="ModLoadContext.LocalizedContent"/>.</summary>
    public TutorialHudSystem(LocalizationManager strings) => _strings = strings;

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        var world = stateRow.World;
        _titleEntity = world.RequireSingleEntityWith<TutorialTitleHudTag>("tutorial title HUD");
        _detailEntity = world.RequireSingleEntityWith<TutorialDetailHudTag>("tutorial detail HUD");
        _statusEntity = world.RequireSingleEntityWith<TutorialStatusHudTag>("tutorial status HUD");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity stateRow, float deltaSeconds)
    {
        _ = deltaSeconds;
        var world = stateRow.World;
        ref readonly var state = ref stateRow.Get<GameState>();

        var header = HeaderText(state);
        var detail = DetailText(state);
        var status = StatusText(state);

        world.Get<BitmapText>(_titleEntity).Content = header;
        world.Get<BitmapText>(_detailEntity).Content = detail;
        world.Get<BitmapText>(_statusEntity).Content = status;
    }

    private string HeaderText(in GameState state) =>
        state.Phase switch
        {
            RoundPhase.Won => _strings.Get("mousechase.round.won"),
            RoundPhase.Lost => _strings.Get("mousechase.round.lost"),
            _ => _strings.Get("mousechase.round.title")
        };

    private string DetailText(in GameState state) =>
        state switch
        {
            { Phase: RoundPhase.Won or RoundPhase.Lost } => _strings.Get("mousechase.round.restart"),
            { EnterZoneSeen: false } => _strings.Get("mousechase.tutorial.enter"),
            { StayZoneSeen: false } => _strings.Get("mousechase.tutorial.stay"),
            { ExitZoneSeen: false } => _strings.Get("mousechase.tutorial.exit"),
            { LocaleSpriteSeen: false } => _strings.Get("mousechase.tutorial.locale"),
            _ => _strings.Get("mousechase.tutorial.complete")
        };

    private static string StatusText(in GameState state) =>
        $"Score {state.Score}/{state.TargetScore}  Health {state.Health:0}  Time {state.TimerSeconds:0.0}s";
}
