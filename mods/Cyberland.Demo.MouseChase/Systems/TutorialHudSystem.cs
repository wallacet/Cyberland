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

        UpdateHudText(world, _titleEntity, header, new Vector2D<float>(40f, 36f), 24f);
        UpdateHudText(world, _detailEntity, detail, new Vector2D<float>(40f, 74f), 18f);
        UpdateHudText(world, _statusEntity, status, new Vector2D<float>(40f, 108f), 18f);
    }

    private void UpdateHudText(World world, EntityId entity, string text, Vector2D<float> viewportPos, float size)
    {
        ref var transform = ref world.Get<Transform>(entity);
        transform.LocalPosition = viewportPos;

        ref var bt = ref world.Get<BitmapText>(entity);
        bt.Content = text;
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
