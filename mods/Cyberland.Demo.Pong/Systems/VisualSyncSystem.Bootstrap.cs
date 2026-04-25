using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using TextureId = System.UInt32;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo.Pong;

public sealed partial class VisualSyncSystem
{
    private void ConfigureSpritesOnStart(World world, TextureId whiteTextureId, TextureId normalTextureId)
    {
        // Every sprite uses world space (the default): HUD bars author positions as "fb.Y - offset" in the
        // camera's virtual viewport, which the camera transform Y-flips back to "offset from top" in screen
        // space — same math as the pre-camera engine.
        ConfigureSprite(world, _v.Background, (int)SpriteLayer.Background, 0f, whiteTextureId, normalTextureId, new Vector4D<float>(0.04f, 0.05f, 0.08f, 1f));
        ConfigureSprite(world, _v.TitleBar, (int)SpriteLayer.Ui, 1f, whiteTextureId, normalTextureId, new Vector4D<float>(0.1f, 0.85f, 0.95f, 1f));
        ConfigureSprite(world, _v.HintBar, (int)SpriteLayer.Ui, 5f, whiteTextureId, normalTextureId, new Vector4D<float>(0.5f, 0.55f, 0.65f, 1f), transparent: true, alpha: 0.85f);
        ConfigureSprite(world, _v.ScorePlayer, (int)SpriteLayer.Ui, 4f, whiteTextureId, normalTextureId, new Vector4D<float>(0.2f, 0.9f, 1f, 1f), new Vector3D<float>(0.2f, 0.85f, 1f), 0.3f);
        ConfigureSprite(world, _v.ScoreCpu, (int)SpriteLayer.Ui, 4f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 0.35f, 0.4f, 1f), new Vector3D<float>(1f, 0.4f, 0.45f), 0.25f);
        ConfigureSprite(world, _v.LeftPad, (int)SpriteLayer.World, 2f, whiteTextureId, normalTextureId, new Vector4D<float>(0.3f, 0.85f, 1f, 1f), new Vector3D<float>(0.3f, 0.9f, 1f), 0.4f);
        ConfigureSprite(world, _v.RightPad, (int)SpriteLayer.World, 2f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 0.35f, 0.45f, 1f), new Vector3D<float>(1f, 0.4f, 0.5f), 0.25f);
        ConfigureSprite(world, _v.Ball, (int)SpriteLayer.World, 3f, whiteTextureId, normalTextureId, new Vector4D<float>(1f, 1f, 1f, 1f), new Vector3D<float>(1f, 1f, 1f), 0.9f);
    }

    private static void ConfigureSprite(
        World world,
        EntityId entity,
        int layer,
        float sortKey,
        TextureId albedoTextureId,
        TextureId normalTextureId,
        Vector4D<float> colorMultiply,
        Vector3D<float>? emissiveTint = null,
        float emissiveIntensity = 0f,
        bool transparent = false,
        float alpha = 1f)
    {
        ref var sprite = ref world.Components<Sprite>().Get(entity);
        sprite.Visible = false;
        sprite.Layer = layer;
        sprite.SortKey = sortKey;
        sprite.AlbedoTextureId = albedoTextureId;
        sprite.NormalTextureId = normalTextureId;
        sprite.ColorMultiply = colorMultiply;
        sprite.Transparent = transparent;
        sprite.Alpha = alpha;
        sprite.EmissiveTint = emissiveTint ?? default;
        sprite.EmissiveIntensity = emissiveIntensity;
    }

    private void ConfigureTextRowsOnStart(World world)
    {
        ConfigureTextRow(world, _t.Title, TitleStyle);
        ConfigureTextRow(world, _t.GameOverLine, GameOverStyle);
        ConfigureTextRow(world, _t.Hint, HintStyle);
        ConfigureTextRow(world, _t.ScoreYou, HudStyle);
        ConfigureTextRow(world, _t.ScorePlayerNum, NumberStyle);
        ConfigureTextRow(world, _t.ScoreCpuLabel, HudStyle);
        ConfigureTextRow(world, _t.ScoreCpuNum, NumberStyle);
    }

    private static void ConfigureTextRow(World world, EntityId entity, TextStyle style)
    {
        ref var text = ref world.Components<BitmapText>().Get(entity);
        text.Style = style;
        text.Visible = false;
        text.CoordinateSpace = CoordinateSpace.ViewportSpace;
        text.SortKey = 450f;
    }
}
