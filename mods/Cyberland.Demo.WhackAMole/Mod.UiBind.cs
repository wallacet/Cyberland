using Cyberland.Demo.WhackAMole.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.WhackAMole;

public sealed partial class Mod
{
    private static HudDocumentRefs ResolveHudRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudRoot = world.RequireSingleEntityWith<HudRootTag>("WhackAMole HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudRoot, out var ids))
            throw new InvalidOperationException("WhackAMole HUD document was not attached from JSON.");

        return new HudDocumentRefs
        {
            Score = ids.Require<UiTextBlock>("hud.score"),
            Timer = ids.Require<UiTextBlock>("hud.timer"),
            Overlay = ids.Require<UiTextBlock>("hud.overlay")
        };
    }
}
