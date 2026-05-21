using Cyberland.Demo.Rts.Components;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Demo.Rts;

/// <summary>Procedural checkerboard texture, unit spawn, and session entity wiring after JSON spawn.</summary>
public sealed partial class Mod
{
    public static void WirePlayfieldAfterSpawn(ModLoadContext context)
    {
        var renderer = context.Host.Renderer ?? throw new InvalidOperationException("Renderer required for RTS demo.");
        var world = context.World;

        var bgTex = BuildCheckerboardTexture();
        var bgTextureId = renderer.RegisterTextureRgba(bgTex, 64, 64);
        if (bgTextureId == TextureId.MaxValue)
            throw new InvalidOperationException("RTS demo failed to register background texture.");

        var backgroundEntity = world.RequireSingleEntityWith<BackgroundTag>("RTS background");
        ConfigureBackgroundSprite(world, renderer, backgroundEntity, bgTextureId);

        var cameraEntity = world.RequireSingleEntityWith<CameraTag>("RTS camera");
        var sessionEntity = world.RequireSingleEntityWith<SessionState>("RTS session");
        world.GetOrAdd<SessionState>(sessionEntity) = new SessionState
        {
            CameraEntity = cameraEntity,
            SelectionBar0 = default,
            SelectionBar1 = default,
            SelectionBar2 = default,
            SelectionBar3 = default,
            BoxDragActive = false,
            BoxDragStartWorld = default,
            BoxDragEndWorld = default,
            PendingCameraFocus = false,
            PendingCameraFocusWorld = default,
            ActiveGroupIndex = SessionState.NoActiveGroup,
            LastGroupKeyIndex = SessionState.NoActiveGroup,
            LastGroupKeyTime = 0f,
            GroupKeyClock = 0f
        };
        _ = world.GetOrAdd<ControlGroups>(sessionEntity);

        ref var session = ref world.Get<SessionState>(sessionEntity);
        foreach (var chunk in world.QueryChunks(SystemQuerySpec.All<SelectionBarTag>()))
        {
            var tags = chunk.Column<SelectionBarTag>();
            var entities = chunk.Entities;
            for (var i = 0; i < entities.Length; i++)
            {
                ref var spr = ref world.Get<Sprite>(entities[i]);
                spr.AlbedoTextureId = renderer.WhiteTextureId;
                spr.NormalTextureId = renderer.DefaultNormalTextureId;

                switch (tags[i].Index)
                {
                    case 0: session.SelectionBar0 = entities[i]; break;
                    case 1: session.SelectionBar1 = entities[i]; break;
                    case 2: session.SelectionBar2 = entities[i]; break;
                    case 3: session.SelectionBar3 = entities[i]; break;
                    default:
                        throw new InvalidOperationException($"RTS selection bar index {tags[i].Index} out of range.");
                }
            }
        }
    }

    private static void ConfigureBackgroundSprite(
        World world,
        IRenderer renderer,
        EntityId backgroundEntity,
        TextureId bgTextureId)
    {
        ref var bgSpr = ref world.Get<Sprite>(backgroundEntity);
        bgSpr.Visible = true;
        bgSpr.Transparent = false;
        bgSpr.AlbedoTextureId = bgTextureId;
        bgSpr.NormalTextureId = renderer.DefaultNormalTextureId;
        bgSpr.HalfExtents = new Vector2D<float>(Constants.PlaySize * 0.5f, Constants.PlaySize * 0.5f);
        bgSpr.ColorMultiply = new Vector4D<float>(0.35f, 0.38f, 0.48f, 1f);
        bgSpr.Layer = (int)SpriteLayer.World;
        bgSpr.SortKey = -500f;
    }

    private static byte[] BuildCheckerboardTexture()
    {
        const int w = 64;
        const int h = 64;
        var rgba = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var cell = ((x >> 3) + (y >> 3)) & 1;
                var i = (y * w + x) * 4;
                var v = cell == 0 ? (byte)56 : (byte)88;
                rgba[i] = v;
                rgba[i + 1] = (byte)(v + 6);
                rgba[i + 2] = (byte)(v + 12);
                rgba[i + 3] = 255;
            }
        }

        return rgba;
    }
}
