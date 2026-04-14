using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Snake;

/// <summary>Maps <see cref="SnakeSession"/> into <see cref="Position"/> + <see cref="Sprite"/> and HUD <see cref="BitmapText"/> for the engine draw passes.</summary>
public sealed class SnakeVisualSyncSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;
    private readonly SnakeSession _session;
    private readonly SnakeVisualBundle _v;

    public SnakeVisualSyncSystem(GameHostServices host, SnakeSession session, SnakeVisualBundle visuals)
    {
        _host = host;
        _session = session;
        _v = visuals;
    }

    public void OnLateUpdate(World world, float deltaSeconds)
    {
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

        var segIdx = 0;
        if (s.Snake.Count > 0)
        {
            var headCell = s.Snake.First!.Value;
            foreach (var seg in s.Snake)
            {
                var e = _v.Segments[segIdx++];
                var center = s.CellCenterWorld(seg.x, seg.y, fb);
                var half = cell * 0.45f;
                var head = seg.x == headCell.x && seg.y == headCell.y;
                ref var pos = ref world.Components<Position>().Get(e);
                pos.X = center.X;
                pos.Y = center.Y;
                ref var spr = ref world.Components<Sprite>().Get(e);
                spr.Visible = true;
                spr.HalfExtents = new Vector2D<float>(half, half);
                spr.Layer = (int)SpriteLayer.World;
                spr.SortKey = 10f + seg.x;
                spr.AlbedoTextureId = white;
                spr.NormalTextureId = n;
                spr.ColorMultiply = head
                    ? new Vector4D<float>(0.2f, 1f, 0.35f, 1f)
                    : new Vector4D<float>(0.05f, 0.55f, 0.12f, 1f);
                spr.Alpha = 1f;
                spr.EmissiveIntensity = head ? 0.5f : 0.1f;
                spr.EmissiveTint = new Vector3D<float>(0.2f, 1f, 0.4f);
                spr.Transparent = false;
            }
        }

        for (var i = segIdx; i < _v.Segments.Length; i++)
        {
            ref var spr = ref world.Components<Sprite>().Get(_v.Segments[i]);
            spr.Visible = false;
        }

        {
            var foodCenter = s.CellCenterWorld(s.Food.x, s.Food.y, fb);
            var half = cell * 0.35f;
            ref var pos = ref world.Components<Position>().Get(_v.Food);
            pos.X = foodCenter.X;
            pos.Y = foodCenter.Y;
            ref var spr = ref world.Components<Sprite>().Get(_v.Food);
            spr.Visible = true;
            spr.HalfExtents = new Vector2D<float>(half, half);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 50f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.2f, 0.25f, 1f);
            spr.Alpha = 1f;
            spr.EmissiveIntensity = 0.8f;
            spr.EmissiveTint = new Vector3D<float>(1f, 0.3f, 0.35f);
            spr.Transparent = false;
        }

        if (s.Phase == SnakePhase.Title)
        {
            var titleBar = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y - 48f), fb);
            ref var pos = ref world.Components<Position>().Get(_v.TitleBar);
            pos.X = titleBar.X;
            pos.Y = titleBar.Y;
            ref var spr = ref world.Components<Sprite>().Get(_v.TitleBar);
            spr.Visible = true;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 20f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 100f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.3f, 1f, 0.5f, 1f);
            spr.Alpha = 1f;
            spr.EmissiveIntensity = 0.6f;
            spr.EmissiveTint = new Vector3D<float>(0.3f, 1f, 0.5f);
            spr.Transparent = false;
        }
        else
        {
            ref var spr = ref world.Components<Sprite>().Get(_v.TitleBar);
            spr.Visible = false;
        }

        if (s.Phase == SnakePhase.GameOver)
        {
            var goPanel = WorldScreenSpace.ScreenPixelToWorldCenter(new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f), fb);
            ref var posG = ref world.Components<Position>().Get(_v.GoPanel);
            posG.X = goPanel.X;
            posG.Y = goPanel.Y;
            ref var sprG = ref world.Components<Sprite>().Get(_v.GoPanel);
            sprG.Visible = true;
            sprG.HalfExtents = new Vector2D<float>(fb.X * 0.45f, 80f);
            sprG.Layer = (int)SpriteLayer.Ui;
            sprG.SortKey = 200f;
            sprG.AlbedoTextureId = white;
            sprG.NormalTextureId = n;
            sprG.ColorMultiply = new Vector4D<float>(0.15f, 0.15f, 0.18f, 1f);
            sprG.Alpha = 0.92f;
            sprG.EmissiveIntensity = 0f;
            sprG.Transparent = true;

            var scoreW = fb.X * 0.45f * Math.Min(1f, s.FoodsEaten / 30f);
            var scoreBar = WorldScreenSpace.ScreenPixelToWorldCenter(
                new Vector2D<float>(fb.X * 0.5f - (fb.X * 0.45f - scoreW) * 0.5f, fb.Y * 0.5f + 28f), fb);
            ref var posS = ref world.Components<Position>().Get(_v.ScoreBar);
            posS.X = scoreBar.X;
            posS.Y = scoreBar.Y;
            ref var sprS = ref world.Components<Sprite>().Get(_v.ScoreBar);
            sprS.Visible = true;
            sprS.HalfExtents = new Vector2D<float>(Math.Max(0.5f, scoreW * 0.5f), 10f);
            sprS.Layer = (int)SpriteLayer.Ui;
            sprS.SortKey = 201f;
            sprS.AlbedoTextureId = white;
            sprS.NormalTextureId = n;
            sprS.ColorMultiply = new Vector4D<float>(1f, 0.85f, 0.2f, 1f);
            sprS.Alpha = 1f;
            sprS.EmissiveIntensity = 0.4f;
            sprS.EmissiveTint = new Vector3D<float>(1f, 0.9f, 0.2f);
            sprS.Transparent = false;
        }
        else
        {
            ref var sprG = ref world.Components<Sprite>().Get(_v.GoPanel);
            sprG.Visible = false;
            ref var sprS = ref world.Components<Sprite>().Get(_v.ScoreBar);
            sprS.Visible = false;
        }

        SetHudText(world, fb, in s);
    }

    private void SetHudText(World world, Vector2D<int> fb, in SnakeSession s)
    {
        var titleSt = new TextStyle(BuiltinFonts.UiSans, 24f, new Vector4D<float>(0.25f, 1f, 0.45f, 1f), Bold: true);
        var hintSt = new TextStyle(BuiltinFonts.UiSans, 14f, new Vector4D<float>(0.55f, 0.65f, 0.6f, 0.9f));
        var hudSt = new TextStyle(BuiltinFonts.UiSans, 16f, new Vector4D<float>(0.85f, 1f, 0.9f, 1f));
        var numSt = new TextStyle(BuiltinFonts.Mono, 18f, new Vector4D<float>(0.95f, 1f, 0.85f, 1f));
        var goSt = new TextStyle(BuiltinFonts.UiSans, 20f, new Vector4D<float>(1f, 0.45f, 0.35f, 1f), Italic: true,
            Underline: true);

        HideAllHudText(world, _v);

        if (s.Phase == SnakePhase.Title)
        {
            SetHudRow(world, _v.TxtTitle, titleSt, "demo.snake.title", true, 36f, fb.Y - 50f);
            SetHudRow(world, _v.TxtHintTitle, hintSt, "demo.snake.hint_title", true, 36f, 100f);
        }
        else if (s.Phase == SnakePhase.GameOver)
        {
            SetHudRow(world, _v.TxtGameOver, goSt, "demo.snake.game_over", true, fb.X * 0.5f - 120f, fb.Y * 0.5f + 52f);
            SetHudRow(world, _v.TxtHintGo, hintSt, "demo.snake.hint_gameover", true, 36f, 118f);
        }
        else if (s.Phase == SnakePhase.Playing)
        {
            SetHudRow(world, _v.TxtPlaying, hudSt, "demo.snake.playing", true, 24f, fb.Y - 36f);
            SetHudRow(world, _v.TxtScore, numSt, s.FoodsEaten.ToString(), false, 110f, fb.Y - 36f);
        }
    }

    private static void HideAllHudText(World world, SnakeVisualBundle v)
    {
        foreach (var e in new[]
                 {
                     v.TxtTitle, v.TxtHintTitle, v.TxtGameOver, v.TxtHintGo, v.TxtPlaying, v.TxtScore
                 })
        {
            ref var bt = ref world.Components<BitmapText>().Get(e);
            bt.Visible = false;
        }
    }

    private static void SetHudRow(World world, EntityId e, TextStyle style, string content, bool isKey, float screenX,
        float screenY)
    {
        ref var pos = ref world.Components<Position>().Get(e);
        pos.X = screenX;
        pos.Y = screenY;
        ref var bt = ref world.Components<BitmapText>().Get(e);
        bt.Visible = true;
        bt.Style = style;
        bt.Content = content;
        bt.IsLocalizationKey = isKey;
        bt.BaselineWorldSpace = false;
        bt.SortKey = 450f;
    }
}
