using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;

namespace Cyberland.Demo.MouseChase.Systems;

/// <summary>
/// Writes tutorial/FPS copy into the retained HUD document created in <see cref="SceneSetup"/>.
/// </summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class HudUiSystem : ISingletonSystem, ISingletonLateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GameState>();

    private readonly LocalizationManager _strings;
    private readonly GameHostServices _host;
    private readonly HudDocumentRefs _hud;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    public HudUiSystem(LocalizationManager strings, GameHostServices host, HudDocumentRefs hud)
    {
        _strings = strings;
        _host = host;
        _hud = hud;
    }

    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        if (!stateRow.World.Components<MouseChaseHudRootTag>().Contains(_hud.RootEntity))
            throw new InvalidOperationException("MouseChase HUD root tag missing; SceneSetup must register UiDocumentRoot.");
    }

    public void OnSingletonLateUpdate(in SingletonEntity stateRow, float deltaSeconds)
    {
        ref readonly var state = ref stateRow.Get<GameState>();

        _hud.Title.Text = HeaderText(state);
        _hud.Detail.Text = DetailText(state);
        _hud.Status.Text = StatusText(state);

        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        _hud.Fps.Text = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS -";
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
