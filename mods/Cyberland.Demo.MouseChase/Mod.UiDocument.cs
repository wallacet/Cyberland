using Cyberland.Demo.MouseChase.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Cyberland.Engine.UI.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo.MouseChase;

/// <summary>Retained HUD document for Mouse Chase; world layout lives in <c>Scenes/demo_mousechase.json</c>.</summary>
public sealed partial class Mod
{
    public const float TutorialTitleHudSize = 24f;
    public const float TutorialDetailHudSize = 18f;
    public const float TutorialStatusHudSize = 18f;

    private static HudDocumentRefs BuildHudDocument(ModLoadContext context)
    {
        var world = context.World;
        var rootEntity = world.RequireSingleEntityWith<MouseChaseHudRootTag>("Mouse Chase HUD root");
        var doc = new UiDocument();

        var title = new UiTextBlock
        {
            Text = " ",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, TutorialTitleHudSize, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(title, 740f, 32f);
        title.AnchoredPosition = new Vector2D<float>(40f, 36f);

        var detail = new UiTextBlock
        {
            Text = " ",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, TutorialDetailHudSize, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(detail, 860f, 28f);
        detail.AnchoredPosition = new Vector2D<float>(40f, 74f);

        var status = new UiTextBlock
        {
            Text = " ",
            DefaultStyle = new TextStyle(BuiltinFonts.UiSans, TutorialStatusHudSize, new Vector4D<float>(1f, 1f, 1f, 1f))
        };
        UiLayoutPresets.TopLeftFixed(status, 900f, 24f);
        status.AnchoredPosition = new Vector2D<float>(40f, 108f);

        var fps = new UiTextBlock
        {
            Text = "FPS -",
            DefaultStyle = new TextStyle(BuiltinFonts.Mono, 14f, new Vector4D<float>(0.4f, 0.88f, 0.52f, 0.9f))
        };
        UiLayoutPresets.TopRightFixed(fps, 120f, 22f, 14f);

        doc.Root.AddChild(title);
        doc.Root.AddChild(detail);
        doc.Root.AddChild(status);
        doc.Root.AddChild(fps);

        context.Host.UiDocuments.Register(rootEntity, doc);

        return new HudDocumentRefs
        {
            RootEntity = rootEntity,
            Title = title,
            Detail = detail,
            Status = status,
            Fps = fps
        };
    }
}
