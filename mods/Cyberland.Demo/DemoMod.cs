using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene2D;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Optional shipped mod: 2D HDR sprite sample + parallel velocity damp ECS.
/// Enable in <c>manifest.json</c> when testing; see repo README.
/// </summary>
/// <remarks>
/// Renderer usage is correct for the engine pipeline: <see cref="GameHostServices.Renderer"/> is the same
/// <see cref="IRenderer"/> the host uses each frame. <see cref="IRenderer.SetGlobalPostProcess"/> sets baseline
/// bloom/tonemap; <see cref="IRenderer.SubmitPostProcessVolume"/> merges overrides (here, full-frame AABB) into
/// <see cref="PostProcessVolumeMerge"/> and the renderer records exactly one HDR pass, one bloom chain, and one
/// composite pass per frame — bloom is not applied twice by the mod.
/// Stationary world lights (ambient + directional + point + spot) are submitted each frame by
/// <see cref="DemoStationaryLightsSystem"/>; the 2D lit shader uses one of each type per frame.
/// </remarks>
public sealed class DemoMod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        var w = context.World;
        var player = w.CreateEntity();
        w.Components<Velocity>().GetOrAdd(player);
        w.Components<Position>().GetOrAdd(player);

        var host = context.Host;
        var r = host.Renderer;
        var white = r?.WhiteTextureId ?? 0;
        var defN = r?.DefaultNormalTextureId ?? 0;
        var h = DemoPlayerConstants.SpriteHalfExtent;

        ref var ps = ref w.Components<Sprite>().GetOrAdd(player);
        ps = Sprite.DefaultWhiteUnlit(white, defN, new Vector2D<float>(h, h));
        ps.Layer = (int)SpriteLayer.World;
        ps.SortKey = 2f;
        ps.ColorMultiply = new Vector4D<float>(0f, 0.9f, 0f, 1f);

        var background = w.CreateEntity();
        w.Components<Position>().GetOrAdd(background);
        w.Components<Sprite>().GetOrAdd(background);

        var neon = w.CreateEntity();
        w.Components<Position>().GetOrAdd(neon);
        w.Components<Sprite>().GetOrAdd(neon);

        ApplyViewportDecor(w, host, background, neon);

        ApplyDemoGlobalPost(host);

        context.RegisterSequential("cyberland.demo/lights", new DemoStationaryLightsSystem(host));
        context.RegisterSequential("cyberland.demo/input", new DemoInputSystem(host, player, context.Scheduler));
        context.RegisterSequential("cyberland.demo/integrate", new DemoIntegrateSystem(host, player));
        // Post volumes merge into one full-frame pass (no per-pixel masking); drive bloom from player X so motion is visible.
        context.RegisterSequential("cyberland.demo/post-volumes",
            new DelegateSequentialSystem((world, _) => SubmitDemoBloomVolume(world, player, host)));
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());
    }

    public void OnUnload()
    {
    }

    /// <summary>
    /// One-shot layout from initial <see cref="IRenderer.SwapchainPixelSize"/> (background + neon strip are static decor).
    /// </summary>
    private static void ApplyViewportDecor(World world, GameHostServices host, EntityId background, EntityId neonStrip)
    {
        var ren = host.Renderer;
        if (ren is null)
            return;

        var fb = ren.SwapchainPixelSize;
        var white = ren.WhiteTextureId;
        var n = ren.DefaultNormalTextureId;

        {
            ref var pos = ref world.Components<Position>().Get(background);
            pos.X = fb.X * 0.5f;
            pos.Y = fb.Y * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(background);
            spr.HalfExtents = new Vector2D<float>(fb.X * 0.5f, fb.Y * 0.5f);
            spr.Layer = (int)SpriteLayer.Background;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.07f, 0.06f, 0.12f, 1f);
            spr.SortKey = 0f;
            spr.Visible = true;
        }

        {
            ref var pos = ref world.Components<Position>().Get(neonStrip);
            pos.X = 110f;
            pos.Y = fb.Y * 0.5f;
            ref var spr = ref world.Components<Sprite>().Get(neonStrip);
            spr.HalfExtents = new Vector2D<float>(36f, 140f);
            spr.Layer = (int)SpriteLayer.World;
            spr.AlbedoTextureId = white;
            spr.NormalTextureId = n;
            spr.ColorMultiply = new Vector4D<float>(0.15f, 0.95f, 0.85f, 1f);
            spr.EmissiveTint = new Vector3D<float>(0.35f, 1f, 0.9f);
            spr.EmissiveIntensity = 2.4f;
            spr.SortKey = 1f;
            spr.Visible = true;
        }
    }

    private static void ApplyDemoGlobalPost(GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
            return;

        r.SetGlobalPostProcess(new GlobalPostProcessSettings
        {
            BloomEnabled = true,
            BloomRadius = 1.5f,
            BloomGain = 1.1f,
            EmissiveToHdrGain = 0.45f,
            EmissiveToBloomGain = 0.6f,
            Exposure = 1f,
            Saturation = 1.05f,
            TonemapEnabled = true,
            ColorGradingShadows = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingMidtones = new Vector3D<float>(1f, 1f, 1f),
            ColorGradingHighlights = new Vector3D<float>(1f, 1f, 1f)
        });
    }

    /// <summary>
    /// Submits a <see cref="PostProcessVolume"/> whose bloom multiplier tracks player X. The renderer applies merged
    /// post to the <strong>entire</strong> frame (volume AABB only gates which overrides win vs global settings), so
    /// animating <see cref="PostProcessOverrides.BloomGain"/> is what makes the effect respond to movement.
    /// </summary>
    private static void SubmitDemoBloomVolume(World world, EntityId player, GameHostServices host)
    {
        var r = host.Renderer;
        if (r is null)
            return;

        var fb = r.SwapchainPixelSize;
        ref readonly var pos = ref world.Components<Position>().Get(player);
        var t = fb.X > 0 ? pos.X / fb.X : 0f;
        t = Math.Clamp(t, 0f, 1f);
        // Merged pp.bloom (global × volume) scales additive bloom after the half-res Gaussian blur; animate for visible change.
        // Left (neon): higher multiplier → brighter glow; right: lower.
        var bloomMul = 2.35f - 1.85f * t;

        r.SubmitPostProcessVolume(new PostProcessVolume
        {
            MinWorld = new Vector2D<float>(0f, 0f),
            MaxWorld = new Vector2D<float>(fb.X, fb.Y),
            Priority = 1,
            Overrides = new PostProcessOverrides
            {
                HasBloomGain = true,
                BloomGain = bloomMul,
                HasExposure = false,
                Exposure = 1f,
                HasSaturation = false,
                Saturation = 1f
            }
        });
    }
}
