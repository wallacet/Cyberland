using Cyberland.Demo.SpriteGallery.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.SpriteGallery;

public sealed partial class Mod
{
    private static HudDocumentRefs ResolveHudRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudEntity = world.RequireSingleEntityWith<HudRootTag>("Sprite Gallery HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudEntity, out var ids))
            throw new InvalidOperationException("Sprite Gallery HUD document was not attached from JSON.");

        return new HudDocumentRefs
        {
            RootEntity = hudEntity,
            Fps = ids.Require<UiTextBlock>("hud.fps")
        };
    }
}
