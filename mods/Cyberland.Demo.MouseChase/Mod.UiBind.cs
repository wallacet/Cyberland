using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.MouseChase;

public sealed partial class Mod
{
    private static HudDocumentRefs ResolveHudRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudEntity = world.RequireSingleEntityWith<MouseChaseHudRootTag>("Mouse Chase HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudEntity, out var ids))
            throw new InvalidOperationException("Mouse Chase HUD document was not attached from JSON.");

        return new HudDocumentRefs
        {
            RootEntity = hudEntity,
            Title = ids.Require<UiTextBlock>("hud.title"),
            Detail = ids.Require<UiTextBlock>("hud.detail"),
            Status = ids.Require<UiTextBlock>("hud.status"),
            Fps = ids.Require<UiTextBlock>("hud.fps")
        };
    }
}
