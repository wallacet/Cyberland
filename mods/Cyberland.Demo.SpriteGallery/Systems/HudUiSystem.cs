using Cyberland.Demo.SpriteGallery.Components;
using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;

namespace Cyberland.Demo.SpriteGallery.Systems;

/// <summary>Updates gallery HUD FPS readout; static title and locale hint use <c>locKey</c> in JSON.</summary>
[RunBefore("cyberland.engine/ui-document-frame")]
public sealed class HudUiSystem : ISingletonSystem, ISingletonLateUpdate
{
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<GalleryState>();

    private readonly GameHostServices _host;
    private readonly HudDocumentRefs _hud;
    private readonly FpsMovingAverage _fps = new(FpsMovingAverage.DefaultWindowSeconds);

    public HudUiSystem(GameHostServices host, HudDocumentRefs hud)
    {
        _host = host;
        _hud = hud;
    }

    public void OnSingletonStart(in SingletonEntity stateRow)
    {
        if (!stateRow.World.Components<HudRootTag>().Contains(_hud.RootEntity))
            throw new InvalidOperationException("Sprite Gallery HUD root tag missing; scene JSON must register ui-document-root.");
    }

    public void OnSingletonLateUpdate(in SingletonEntity stateRow, float deltaSeconds)
    {
        _ = stateRow;
        var frame = _host.LastPresentDeltaSeconds > 1e-6f ? _host.LastPresentDeltaSeconds : deltaSeconds;
        _fps.AddFrameDeltaSeconds(frame);
        _hud.Fps.Text = _fps.TryGetAverageFps(out var f) ? $"FPS {MathF.Round(f)}" : "FPS -";
    }
}
