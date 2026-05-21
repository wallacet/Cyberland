using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.Rts;

public sealed partial class Mod
{
    private static HudDocumentRefs ResolveHudRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudRoot = world.RequireSingleEntityWith<HudRootTag>("RTS HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudRoot, out var ids))
            throw new InvalidOperationException("RTS HUD document was not attached from JSON.");

        return new HudDocumentRefs
        {
            RootEntity = hudRoot,
            Fps = ids.Require<UiTextBlock>("hud.fps")
        };
    }
}
