using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.BrickBreaker;

/// <summary>Maps <see cref="BrickSession"/> to ECS sprites for the engine draw pass.</summary>
public sealed class BrickVisualSyncSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly BrickSession _session;
    private readonly EntityId _background;
    private readonly EntityId _paddle;
    private readonly EntityId _ball;
    private readonly EntityId _titleUi;
    private readonly EntityId _gameOverPanel;
    private readonly EntityId _gameOverBar;
    private readonly EntityId[] _lives;
    private readonly EntityId[,] _cells;

    public BrickVisualSyncSystem(
        GameHostServices host,
        BrickSession session,
        EntityId background,
        EntityId paddle,
        EntityId ball,
        EntityId titleUi,
        EntityId gameOverPanel,
        EntityId gameOverBar,
        EntityId[] lives,
        EntityId[,] cells)
    {
        _host = host;
        _session = session;
        _background = background;
        _paddle = paddle;
        _ball = ball;
        _titleUi = titleUi;
        _gameOverPanel = gameOverPanel;
        _gameOverBar = gameOverBar;
        _lives = lives;
        _cells = cells;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        var s = _session;
        var fb = r.SwapchainPixelSize;
        var white = r.WhiteTextureId;
        var n = r.DefaultNormalTextureId;

        {
            ref var pos = ref world.Components<Position>().Get(_background);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_background);
            spr.Visible = true;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
            spr.Layer = (int)SpriteLayer.Background;
            spr.SortKey = 0f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.04f, 0.04f, 0.08f, 1f);
            spr.EmissiveIntensity = 0f;
        }

        for (var cx = 0; cx < BrickConstants.Cols; cx++)
        for (var cy = 0; cy < BrickConstants.Rows; cy++)
        {
            var e = _cells[cx, cy];
            var on = s.Bricks[cx, cy];
            ref var spr = ref world.Components<Sprite>().Get(e);
            spr.Visible = on;
            if (!on)
                continue;

            var bx = s.BrickOriginX + (cx + 0.5f) * s.BrickW;
            var by = s.BrickTopY - (cy + 0.5f) * s.BrickH;
            var hue = (cx + cy * 0.7f) * 0.08f;
            ref var pos = ref world.Components<Position>().Get(e);
            pos.X = bx;
            pos.Y = by;
            spr.HalfExtents = new Vector2D<float>(s.BrickW * 0.46f, s.BrickH * 0.45f);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = cx + cy * 0.1f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.3f + hue, 0.5f, 1f - hue, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.4f, 0.6f, 1f);
            spr.EmissiveIntensity = 0.15f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_paddle);
            pos.X = s.PaddleCenterX;
            pos.Y = s.PaddleY;
            ref var spr = ref world.Components<Sprite>().Get(_paddle);
            spr.Visible = s.Phase == BrickPhase.Playing;
            spr.HalfExtents = new Vector2D<float>(s.PaddleHalfW, s.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 5f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.4f, 0.85f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.4f, 0.9f, 1f);
            spr.EmissiveIntensity = 0.2f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_ball);
            pos.X = s.BallPos.X;
            pos.Y = s.BallPos.Y;
            ref var spr = ref world.Components<Sprite>().Get(_ball);
            spr.Visible = s.Phase == BrickPhase.Playing;
            spr.HalfExtents = new Vector2D<float>(BrickConstants.BallR, BrickConstants.BallR);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 8f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
            spr.EmissiveIntensity = 0.7f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_titleUi);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y - 56f;
            ref var spr = ref world.Components<Sprite>().Get(_titleUi);
            spr.Visible = s.Phase == BrickPhase.Title;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 18f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 20f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.5f, 0.75f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.5f, 0.8f, 1f);
            spr.EmissiveIntensity = 0.5f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_gameOverPanel);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.45f;
            ref var spr = ref world.Components<Sprite>().Get(_gameOverPanel);
            spr.Visible = s.Phase == BrickPhase.GameOver;
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.42f, 70f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 200f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.12f, 0.12f, 0.16f, 1f);
            spr.Alpha = 0.95f;
            spr.Transparent = true;
            spr.EmissiveIntensity = 0f;
        }

        {
            var wBar = fb.X * 0.4f * Math.Min(1f, s.Score / 600f);
            ref var pos = ref world.Components<Position>().Get(_gameOverBar);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.45f + 36f;
            ref var spr = ref world.Components<Sprite>().Get(_gameOverBar);
            spr.Visible = s.Phase == BrickPhase.GameOver;
            spr.HalfExtents = new Vector2D<float>(Math.Max(8f, wBar * 0.5f), 10f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 201f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.75f, 0.2f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.8f, 0.2f);
            spr.EmissiveIntensity = 0.35f;
        }

        var lifeW = 14f;
        for (var i = 0; i < BrickConstants.StartingLives; i++)
        {
            ref var pos = ref world.Components<Position>().Get(_lives[i]);
            pos.X = 30f + i * (lifeW * 2f);
            pos.Y = fb.Y - 28f;
            ref var spr = ref world.Components<Sprite>().Get(_lives[i]);
            spr.Visible = s.Phase == BrickPhase.Playing && i < s.Lives;
            spr.HalfExtents = new Vector2D<float>(lifeW, 6f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 50f + i;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.9f, 0.3f, 0.35f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.35f, 0.4f);
            spr.EmissiveIntensity = 0.2f;
        }
    }
}
