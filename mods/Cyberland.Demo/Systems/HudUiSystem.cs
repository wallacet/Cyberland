using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo;

/// <summary>Late update: FPS label on the retained HUD document (<c>Content/Ui/hdr_hud.json</c>).</summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class HudUiSystem : ISingletonSystem, ISingletonLateUpdate
{
    /// <inheritdoc />
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<PlayerTag>();

    private readonly GameHostServices _host;
    private readonly HudDocumentRefs _hud;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    /// <summary>Creates the system.</summary>
    public HudUiSystem(GameHostServices host, HudDocumentRefs hud)
    {
        _host = host;
        _hud = hud;
    }

    /// <inheritdoc />
    public void OnSingletonStart(in SingletonEntity playerRow)
    {
        if (!playerRow.World.Components<HudRootTag>().Contains(_hud.RootEntity))
            throw new InvalidOperationException("HDR HUD root tag missing; scene must register ui-document-root.");
    }

    /// <inheritdoc />
    public void OnSingletonLateUpdate(in SingletonEntity playerRow, float deltaSeconds)
    {
        _ = playerRow;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        _hud.Fps.Text = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS —";
    }
}
