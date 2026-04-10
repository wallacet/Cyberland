using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

/// <summary>Copies <see cref="PongState"/> into <see cref="Position"/> + <see cref="Sprite"/> for the engine sprite pass.</summary>
public sealed class PongVisualSyncSystem : ISystem
{
    private readonly GameHostServices _host;
    private readonly EntityId _session;
    private readonly PongVisualIds _v;

    public PongVisualSyncSystem(GameHostServices host, EntityId session, PongVisualIds visuals)
    {
        _host = host;
        _session = session;
        _v = visuals;
    }

    public void OnUpdate(World world, float deltaSeconds)
    {
        _ = deltaSeconds;
        var r = _host.Renderer;
        if (r is null)
            return;

        ref readonly var st = ref world.Components<PongState>().Get(_session);
        var fb = r.SwapchainPixelSize;
        var white = r.WhiteTextureId;
        var n = r.DefaultNormalTextureId;

        SetBg(world, fb, white, n);
        SetTitle(world, fb, st, white, n);
        SetHint(world, fb, st, white, n);
        SetScores(world, fb, st, white, n);
        SetPlaying(world, st, white, n);
    }

    private void SetBg(World world, Vector2D<int> fb, int white, int defN)
    {
        ref var pos = ref world.Components<Position>().Get(_v.Background);
        pos.X = fb.X * 0.5f;
        pos.Y = fb.Y * 0.5f;
        ref var spr = ref world.Components<Sprite>().Get(_v.Background);
        spr.Visible = true;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
        spr.Layer = (int)SpriteLayer.Background;
        spr.SortKey = 0f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.04f, 0.05f, 0.08f, 1f);
        spr.Alpha = 1f;
        spr.EmissiveIntensity = 0f;
    }

    private void SetTitle(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        var titlePulse = 0.85f + 0.15f * MathF.Sin(st.Pulse);
        ref var pos = ref world.Components<Position>().Get(_v.TitleBar);
        pos.X = fb.X * 0.5f;
        pos.Y = fb.Y - 42f;
        ref var spr = ref world.Components<Sprite>().Get(_v.TitleBar);
        spr.Visible = st.Phase is PongPhase.Title or PongPhase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.45f * titlePulse, 18f);
        spr.Layer = (int)SpriteLayer.Ui;
        spr.SortKey = 1f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.1f, 0.85f, 0.95f, 1f);
        spr.Alpha = 1f;
        spr.EmissiveTint = new Vector3D<float>(0.2f, 0.9f, 1f);
        spr.EmissiveIntensity = 1.6f;
    }

    private void SetHint(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        var y = st.Phase == PongPhase.GameOver ? 130f : 100f;
        ref var pos = ref world.Components<Position>().Get(_v.HintBar);
        pos.X = fb.X * 0.5f;
        pos.Y = y;
        ref var spr = ref world.Components<Sprite>().Get(_v.HintBar);
        spr.Visible = st.Phase is PongPhase.Title or PongPhase.GameOver;
        spr.HalfExtents = new Vector2D<float>(fb.X * 0.4f, 14f);
        spr.Layer = (int)SpriteLayer.Ui;
        spr.SortKey = 5f;
        spr.AlbedoTextureId = white;
        spr.NormalTextureId = defN;
        spr.ColorMultiply = new Vector4D<float>(0.5f, 0.55f, 0.65f, 1f);
        spr.Alpha = 0.85f;
        spr.Transparent = true;
        spr.EmissiveIntensity = 0f;
    }

    private void SetScores(World world, Vector2D<int> fb, in PongState st, int white, int defN)
    {
        var maxH = fb.Y * 0.25f;
        var ph = maxH * (st.PlayerPoints / (float)PongConstants.WinScore);
        var ch = maxH * (st.CpuPoints / (float)PongConstants.WinScore);
        var playing = st.Phase == PongPhase.Playing;

        {
            ref var pos = ref world.Components<Position>().Get(_v.ScorePlayer);
            pos.X = 18f;
            pos.Y = 80f + ph * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_v.ScorePlayer);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(10f, ph * 0.5f + 2f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 4f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(0.2f, 0.9f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.2f, 0.85f, 1f);
            spr.EmissiveIntensity = 0.3f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.ScoreCpu);
            pos.X = fb.X - 18f;
            pos.Y = 80f + ch * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(_v.ScoreCpu);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(10f, ch * 0.5f + 2f);
            spr.Layer = (int)SpriteLayer.Ui;
            spr.SortKey = 4f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.35f, 0.4f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.4f, 0.45f);
            spr.EmissiveIntensity = 0.25f;
        }
    }

    private void SetPlaying(World world, in PongState st, int white, int defN)
    {
        var playing = st.Phase == PongPhase.Playing;
        {
            ref var pos = ref world.Components<Position>().Get(_v.LeftPad);
            pos.X = st.ArenaMinX;
            pos.Y = st.LeftPaddleY;
            ref var spr = ref world.Components<Sprite>().Get(_v.LeftPad);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.PaddleHalfW, PongConstants.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 2f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(0.3f, 0.85f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.3f, 0.9f, 1f);
            spr.EmissiveIntensity = 0.4f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.RightPad);
            pos.X = st.ArenaMaxX;
            pos.Y = st.RightPaddleY;
            ref var spr = ref world.Components<Sprite>().Get(_v.RightPad);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.PaddleHalfW, PongConstants.PaddleHalfH);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 2f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 0.35f, 0.45f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 0.4f, 0.5f);
            spr.EmissiveIntensity = 0.25f;
        }

        {
            ref var pos = ref world.Components<Position>().Get(_v.Ball);
            pos.X = st.BallPos.X;
            pos.Y = st.BallPos.Y;
            ref var spr = ref world.Components<Sprite>().Get(_v.Ball);
            spr.Visible = playing;
            spr.HalfExtents = new Vector2D<float>(PongConstants.BallR, PongConstants.BallR);
            spr.Layer = (int)SpriteLayer.World;
            spr.SortKey = 3f;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = defN;
            spr.ColorMultiply = new Vector4D<float>(1f, 1f, 1f, 1f);
            spr.EmissiveTint = new Vector3D<float>(1f, 1f, 1f);
            spr.EmissiveIntensity = 0.9f;
        }
    }
}
