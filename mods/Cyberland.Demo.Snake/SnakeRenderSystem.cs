using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>Snake segments, food, and UI (grid is drawn via engine <see cref="Tilemap"/>).</summary>
public sealed class SnakeRenderSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;
    private readonly LocalizationManager _localization;
    private readonly FontLibrary _fonts;
    private readonly TextGlyphCache _textCache;

    public SnakeRenderSystem(
        GameHostServices host,
        SnakeSession session,
        LocalizationManager localization,
        FontLibrary fonts,
        TextGlyphCache textCache)
    {
        _host = host;
        _session = session;
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

        var s = _session;
        var fb = r.SwapchainPixelSize;
        if (fb.X <= 0 || fb.Y <= 0)
            return;

        s.UpdateLayout(fb.X, fb.Y);

        var white = r.WhiteTextureId;
        var n = r.DefaultNormalTextureId;
        var cell = s.Cell;
        var ox = s.OriginX;
        var oy = s.OriginY;

        if (s.Snake.Count > 0)
        {
            var headCell = s.Snake.First!.Value;
            foreach (var seg in s.Snake)
            {
                var cx = ox + (seg.x + 0.5f) * cell;
                var cy = oy + (seg.y + 0.5f) * cell;
                var half = cell * 0.45f;
                var head = seg.x == headCell.x && seg.y == headCell.y;
                r.SubmitSprite(SnakeSpriteUtil.Q(white, n, new Vector2D<float>(cx, cy), new Vector2D<float>(half, half),
                    (int)SpriteLayer.World, 10f + seg.x,
                    head
                        ? new Vector4D<float>(0.2f, 1f, 0.35f, 1f)
                        : new Vector4D<float>(0.05f, 0.55f, 0.12f, 1f),
                    1f, head ? 0.5f : 0.1f, new Vector3D<float>(0.2f, 1f, 0.4f)));
            }
        }

        {
            var cx = ox + (s.Food.x + 0.5f) * cell;
            var cy = oy + (s.Food.y + 0.5f) * cell;
            var half = cell * 0.35f;
            r.SubmitSprite(SnakeSpriteUtil.Q(white, n, new Vector2D<float>(cx, cy), new Vector2D<float>(half, half),
                (int)SpriteLayer.World, 50f,
                new Vector4D<float>(1f, 0.2f, 0.25f, 1f), 1f, 0.8f, new Vector3D<float>(1f, 0.3f, 0.35f)));
        }

        if (s.Phase == SnakePhase.Title)
        {
            r.SubmitSprite(SnakeSpriteUtil.Q(white, n, new Vector2D<float>(fb.X * 0.5f, fb.Y - 48f),
                new Vector2D<float>(fb.X * 0.42f, 20f), (int)SpriteLayer.Ui, 100f,
                new Vector4D<float>(0.3f, 1f, 0.5f, 1f), 1f, 0.6f, new Vector3D<float>(0.3f, 1f, 0.5f)));
        }
        else if (s.Phase == SnakePhase.GameOver)
        {
            r.SubmitSprite(SnakeSpriteUtil.Q(white, n, new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f),
                new Vector2D<float>(fb.X * 0.45f, 80f), (int)SpriteLayer.Ui, 200f,
                new Vector4D<float>(0.15f, 0.15f, 0.18f, 1f), 0.92f, 0f, default, transparent: true));
            var scoreW = fb.X * 0.45f * Math.Min(1f, s.FoodsEaten / 30f);
            r.SubmitSprite(SnakeSpriteUtil.Q(white, n, new Vector2D<float>(fb.X * 0.5f - (fb.X * 0.45f - scoreW) * 0.5f, fb.Y * 0.5f + 28f),
                new Vector2D<float>(scoreW * 0.5f, 10f), (int)SpriteLayer.Ui, 201f,
                new Vector4D<float>(1f, 0.85f, 0.2f, 1f), 1f, 0.4f, new Vector3D<float>(1f, 0.9f, 0.2f)));
        }

        DrawSnakeText(r, fb, in s);
    }

    private void DrawSnakeText(IRenderer r, Vector2D<int> fb, in SnakeSession s)
    {
        var titleSt = new TextStyle(BuiltinFonts.UiSans, 24f, new Vector4D<float>(0.25f, 1f, 0.45f, 1f), Bold: true);
        var hintSt = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.65f, 0.6f, 0.9f));
        var hudSt = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.85f, 1f, 0.9f, 1f));
        var numSt = new TextStyle(BuiltinFonts.Mono, 18f, new Vector4D<float>(0.95f, 1f, 0.85f, 1f));
        var goSt = new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 0.45f, 0.35f, 1f), Italic: true,
            Underline: true);

        if (s.Phase == SnakePhase.Title)
        {
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, titleSt, "demo.snake.title",
                new Vector2D<float>(36f, fb.Y - 50f));
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hintSt, "demo.snake.hint_title",
                new Vector2D<float>(36f, 100f));
        }
        else if (s.Phase == SnakePhase.GameOver)
        {
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, goSt, "demo.snake.game_over",
                new Vector2D<float>(fb.X * 0.5f - 120f, fb.Y * 0.5f + 52f));
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hintSt, "demo.snake.hint_gameover",
                new Vector2D<float>(36f, 118f));
        }
        else if (s.Phase == SnakePhase.Playing)
        {
            TextRenderer.DrawLocalized(r, _fonts, _textCache, _localization, hudSt, "demo.snake.playing",
                new Vector2D<float>(24f, fb.Y - 36f));
            TextRenderer.DrawLiteral(r, _fonts, _textCache, numSt, s.FoodsEaten.ToString(),
                new Vector2D<float>(110f, fb.Y - 36f));
        }
    }
}
