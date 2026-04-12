using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>Localized HUD captions for the HDR sample (built-in fonts; no mod Content required).</summary>
public sealed class DemoHudTextSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly LocalizationManager _localization;
    private readonly FontLibrary _fonts;
    private readonly TextGlyphCache _textCache;

    public DemoHudTextSystem(
        GameHostServices host,
        LocalizationManager localization,
        FontLibrary fonts,
        TextGlyphCache textCache)
    {
        _host = host;
        _localization = localization;
        _fonts = fonts;
        _textCache = textCache;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
    {
        _ = world;
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        var title = new TextStyle(BuiltinFonts.UiSans, 22f, new Vector4D<float>(0.85f, 0.95f, 1f, 1f), Bold: true);
        var hint = new TextStyle(BuiltinFonts.UiSans, 15f, new Vector4D<float>(0.55f, 0.65f, 0.75f, 0.9f), Italic: true);
        TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, title, "demo.hdr.title",
            new Vector2D<float>(24f, fb.Y - 36f));
        TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hint, "demo.hdr.hint",
            new Vector2D<float>(24f, 48f));
    }
}
