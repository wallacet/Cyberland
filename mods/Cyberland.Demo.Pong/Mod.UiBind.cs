using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Demo.Pong;

public sealed partial class Mod
{
    private static HudDocumentRefs ResolveHudRefs(ModLoadContext context)
    {
        var world = context.World;
        var hudRoot = world.RequireSingleEntityWith<HudRootTag>("Pong HUD root");
        if (!context.Host.UiDocuments.TryGetElements(hudRoot, out var ids))
            throw new InvalidOperationException("Pong HUD document was not attached from JSON.");

        return new HudDocumentRefs
        {
            Title = ids.Require<UiTextBlock>("hud.title"),
            GameOver = ids.Require<UiTextBlock>("hud.game_over"),
            Hint = ids.Require<UiTextBlock>("hud.hint"),
            ScoreYou = ids.Require<UiTextBlock>("hud.score_you"),
            ScorePlayer = ids.Require<UiTextBlock>("hud.score_player"),
            ScoreCpuLabel = ids.Require<UiTextBlock>("hud.score_cpu_label"),
            ScoreCpu = ids.Require<UiTextBlock>("hud.score_cpu"),
            Fps = ids.Require<UiTextBlock>("hud.fps")
        };
    }
}
