using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Sequential late pass: draws entities that have both <see cref="BitmapText"/> and <see cref="Position"/> via <see cref="TextRenderer"/>.
/// </summary>
/// <remarks>
/// Runs after mod systems update labels; register order places this after <see cref="SpriteRenderSystem"/> so typical HUD sort keys stack above world sprites.
/// Glyph rasterization uses a locked cache; this system runs on the main scheduler thread (sequential <see cref="ILateUpdate"/>).
/// </remarks>
public sealed class TextRenderSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <param name="host">Uses <see cref="GameHostServices.Renderer"/>, <see cref="GameHostServices.Fonts"/>, <see cref="GameHostServices.TextGlyphCache"/>, and <see cref="GameHostServices.LocalizedContent"/>.</param>
    public TextRenderSystem(GameHostServices host) =>
        _host = host;

    /// <inheritdoc />
    public void OnLateUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var fonts = _host.Fonts;
        var cache = _host.TextGlyphCache;
        var loc = _host.LocalizedContent?.Strings;
        var fb = r.SwapchainPixelSize;

        foreach (var chunk in world.QueryChunks<BitmapText, Position>())
        {
            var texts = chunk.Components0;
            var positions = chunk.Components1;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var bt = ref texts[i];
                if (!bt.Visible)
                    continue;

                if (string.IsNullOrEmpty(bt.Content))
                    continue;

                ref readonly var pos = ref positions[i];

                if (bt.BaselineWorldSpace)
                {
                    var baselineWorld = pos.AsVector();
                    if (bt.IsLocalizationKey)
                    {
                        if (loc is null)
                            continue;
                        TextRenderer.DrawLocalized(r, fonts, cache, loc, in bt.Style, bt.Content, baselineWorld, bt.SortKey);
                    }
                    else
                    {
                        TextRenderer.DrawLiteral(r, fonts, cache, in bt.Style, bt.Content, baselineWorld, bt.SortKey);
                    }
                }
                else
                {
                    var baselineScreen = pos.AsVector();
                    if (bt.IsLocalizationKey)
                    {
                        if (loc is null)
                            continue;
                        TextRenderer.DrawLocalizedScreen(r, fonts, cache, loc, in bt.Style, bt.Content, baselineScreen, fb,
                            bt.SortKey);
                    }
                    else
                    {
                        TextRenderer.DrawLiteralScreen(r, fonts, cache, in bt.Style, bt.Content, baselineScreen, fb,
                            bt.SortKey);
                    }
                }
            }
        }
    }
}
