using Cyberland.Engine.Rendering;
using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Cyberland.Engine.Tests;

public sealed class RenderingArchitectureContractsTests
{
    [Fact]
    public void FramePlan2D_ctor_assigns_all_fields()
    {
        var sprites = new[] { new SpriteDrawRequest() };
        var pointLights = new[] { new PointLight() };
        var spotLights = new[] { new SpotLight() };
        var directionalLights = new[] { new DirectionalLight() };
        var ambientLights = new[] { new AmbientLight() };
        var volumes = new[] { new PostProcessVolumeSubmission() };
        var global = new GlobalPostProcessSettings { BloomEnabled = true, BloomRadius = 1.2f, BloomGain = 0.25f, Exposure = 1.2f, Saturation = 0.9f };
        var resolved = new GlobalPostProcessSettings { BloomEnabled = false, BloomRadius = 0.8f, BloomGain = 0f, Exposure = 0.75f, Saturation = 1.1f };
        var sortIndices = new[] { 0 };
        var screen = new Vector2D<float>(1920f, 1080f);

        var camera = new CameraViewRequest
        {
            PositionWorld = new Vector2D<float>(640f, 360f),
            RotationRadians = 0.5f,
            ViewportSizeWorld = new Vector2D<int>(1280, 720),
            Priority = 7,
            Enabled = true,
            BackgroundColor = new Vector4D<float>(0.01f, 0.02f, 0.03f, 1f)
        };
        var physical = new PhysicalViewport(new Vector2D<int>(10, 20), new Vector2D<int>(1280, 720), 1.5f);

        var plan = new FramePlan(
            sprites,
            1,
            pointLights,
            1,
            spotLights,
            1,
            directionalLights,
            1,
            ambientLights,
            1,
            volumes,
            1,
            in global,
            in resolved,
            sortIndices,
            transparentSpriteCount: 0,
            in screen,
            in camera,
            in physical);

        Assert.Same(sprites, plan.Sprites);
        Assert.Equal(1, plan.SpriteCount);
        Assert.Same(pointLights, plan.PointLights);
        Assert.Same(spotLights, plan.SpotLights);
        Assert.Same(directionalLights, plan.DirectionalLights);
        Assert.Same(ambientLights, plan.AmbientLights);
        Assert.Same(volumes, plan.Volumes);
        Assert.Equal(global.BloomEnabled, plan.GlobalPost.BloomEnabled);
        Assert.Equal(global.BloomGain, plan.GlobalPost.BloomGain);
        Assert.Equal(global.BloomRadius, plan.GlobalPost.BloomRadius);
        Assert.Equal(global.Exposure, plan.GlobalPost.Exposure);
        Assert.Equal(global.Saturation, plan.GlobalPost.Saturation);
        Assert.Equal(resolved.BloomEnabled, plan.ResolvedPost.BloomEnabled);
        Assert.Equal(resolved.BloomGain, plan.ResolvedPost.BloomGain);
        Assert.Equal(resolved.BloomRadius, plan.ResolvedPost.BloomRadius);
        Assert.Equal(resolved.Exposure, plan.ResolvedPost.Exposure);
        Assert.Equal(resolved.Saturation, plan.ResolvedPost.Saturation);
        Assert.Same(sortIndices, plan.SortIndices);
        Assert.Equal(screen.X, plan.Screen.X);
        Assert.Equal(screen.Y, plan.Screen.Y);
        Assert.Equal(camera.PositionWorld, plan.Camera.PositionWorld);
        Assert.Equal(camera.Priority, plan.Camera.Priority);
        Assert.Equal(camera.ViewportSizeWorld, plan.Camera.ViewportSizeWorld);
        Assert.Equal(physical.OffsetPixels, plan.Physical.OffsetPixels);
        Assert.Equal(physical.SizePixels, plan.Physical.SizePixels);
        Assert.Equal(physical.Scale, plan.Physical.Scale);
    }

    [Fact]
    public void PostEffectContext2D_ctor_assigns_all_fields()
    {
        var cmd = default(CommandBuffer);
        var fb = default(Framebuffer);
        var fullViewport = new Viewport { X = 1f, Y = 2f, Width = 100f, Height = 200f, MinDepth = 0f, MaxDepth = 1f };
        var fullScissor = new Rect2D
        {
            Offset = new Offset2D { X = 3, Y = 4 },
            Extent = new Extent2D { Width = 300, Height = 400 }
        };
        var halfViewport = new Viewport { X = 5f, Y = 6f, Width = 50f, Height = 60f, MinDepth = 0f, MaxDepth = 1f };
        var halfScissor = new Rect2D
        {
            Offset = new Offset2D { X = 7, Y = 8 },
            Extent = new Extent2D { Width = 70, Height = 80 }
        };

        var camera = CameraSelection.Default(new Vector2D<int>(1, 1));
        var physical = new PhysicalViewport(new Vector2D<int>(0, 0), new Vector2D<int>(1, 1), 1f);
        var plan = new FramePlan(
            sprites: [],
            spriteCount: 0,
            pointLights: [],
            pointLightCount: 0,
            spotLights: [],
            spotLightCount: 0,
            directionalLights: [],
            directionalLightCount: 0,
            ambientLights: [],
            ambientLightCount: 0,
            volumes: [],
            volumeCount: 0,
            globalPost: default,
            resolvedPost: default,
            sortIndices: [],
            transparentSpriteCount: 0,
            screen: new Vector2D<float>(1f, 1f),
            camera: camera,
            physical: physical);

        var context = new PostEffectContext(cmd, fb, in plan, in fullViewport, in fullScissor, in halfViewport, in halfScissor);

        Assert.Equal(cmd.Handle, context.Cmd.Handle);
        Assert.Equal(fb.Handle, context.SwapFramebuffer.Handle);
        Assert.Equal(plan.Screen.X, context.FramePlan.Screen.X);
        Assert.Equal(plan.Screen.Y, context.FramePlan.Screen.Y);
        Assert.Equal(fullViewport.Width, context.FullViewport.Width);
        Assert.Equal(fullViewport.Height, context.FullViewport.Height);
        Assert.Equal(fullScissor.Extent.Width, context.FullScissor.Extent.Width);
        Assert.Equal(fullScissor.Extent.Height, context.FullScissor.Extent.Height);
        Assert.Equal(halfViewport.Width, context.HalfViewport.Width);
        Assert.Equal(halfViewport.Height, context.HalfViewport.Height);
        Assert.Equal(halfScissor.Extent.Width, context.HalfScissor.Extent.Width);
        Assert.Equal(halfScissor.Extent.Height, context.HalfScissor.Extent.Height);
    }
}
