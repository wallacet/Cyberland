using Cyberland.Demo.Rts.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.Rts.Systems;

/// <summary>Late update: FPS on retained HUD (<c>Content/Ui/rts_hud.json</c>).</summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class HudUiSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SessionState>();

    private readonly GameHostServices _host;
    private readonly HudDocumentRefs _hud;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    public HudUiSystem(GameHostServices host, HudDocumentRefs hud)
    {
        _host = host;
        _hud = hud;
    }

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity sessionRow)
    {
        if (!sessionRow.World.Components<HudRootTag>().Contains(_hud.RootEntity))
            throw new InvalidOperationException("RTS HUD root missing after scene spawn.");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity sessionRow, float deltaSeconds)
    {
        _ = sessionRow;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        _hud.Fps.Text = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
    }
}
