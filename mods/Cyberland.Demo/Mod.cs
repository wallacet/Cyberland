using Cyberland.Engine;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo;

/// <summary>
/// HDR lighting + post-process tutorial mod: spawns a small 2D scene, drives the player with ECS systems, and shows how
/// sequential vs parallel registration fits a frame (early → fixed → late; parallel fixed uses host <see cref="ParallelOptions"/>).
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> this file lists registration order. Follow each system’s source for phase callbacks;
/// <see cref="SceneSetup"/> holds the static scene “recipe” (<see cref="SceneSetup.SetupSceneAsync"/>). Other Cyberland demos increase
/// gameplay scope (Snake, Pong, BrickBreaker) after you understand this pipeline.</para>
/// <para><b>Frame flow (simplified):</b>
/// <see cref="InputSystem"/> (early, parallel velocity clears/writes; sequential axis read between barriers) →
/// <see cref="IntegrateSystem"/> (fixed, <see cref="ISingletonSystem"/>) moves <see cref="Scene.Transform"/> and clamps to the virtual canvas →
/// <see cref="VelocityDampSystem"/> (fixed, parallel) scales velocity down so motion doesn’t feel frictionless →
/// lighting/post runs in engine/system order →
/// <see cref="HdrPostVolumeFillSystem"/> (late, singleton) retargets the fullscreen bloom volume and gain →
/// <see cref="FpsDisplaySystem"/> (late, singleton) updates HUD text.</para>
/// <para><b>Registration order matters</b> where systems depend on published state: integrate must see input’s velocity,
/// damp runs after integrate in the same fixed phase, and post-volume reads the player transform after hierarchy/lighting
/// have advanced for the tick.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        DemoInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("demo_hdr.json");
        KickoffBuiltinAtlasLoads(context);
        ValidateShaderModuleLoadPaths(context);

        var host = context.Host;

        await SceneSetup.SetupSceneAsync(context);

        // Late: keeps the authored bloom volume centered on the swapchain viewport and ties bloom strength to player X.
        context.RegisterSingleton("cyberland.demo/hdr-post-volume", new HdrPostVolumeFillSystem(host));

        // Early: parallel velocity SoA updates; axis read runs on the scheduler thread between Parallel.ForEach barriers.
        context.RegisterParallel("cyberland.demo/input", new InputSystem(host, context.Scheduler));
        context.RegisterSingleton("cyberland.demo/integrate", new IntegrateSystem(host));

        // Parallel chunk pass example (same fixed phase as integrate; host supplies ParallelOptions for partitioning).
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());

        // Late: HUD overlay; uses presentation viewport size so text stays in the corner when the window scales.
        context.RegisterSingleton("cyberland.demo/fps-display", new FpsDisplaySystem(host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static void KickoffBuiltinAtlasLoads(ModLoadContext context)
    {
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansRegular22);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.UiSansBold23);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
    }

    private static void ValidateShaderModuleLoadPaths(ModLoadContext context)
    {
        // Smoke-test both custom shader pathways during bootstrap:
        // 1) precompiled SPIR-V bytes from mod Content/;
        // 2) GLSL runtime compile fallback when no .spv is supplied.
        var assets = new AssetManager(context.VirtualFileSystem);
        var renderer = context.Host.Renderer;

        var precompiledSpirv = assets.LoadBytes("Shaders/demo_precompiled.vert.glsl.spv");
        using var precompiled = renderer.CreateShaderModuleFromSpirv(
            precompiledSpirv,
            "shader.Demo.Precompiled.Vert");

        var fallbackGlsl = assets.LoadTextAsync("Shaders/demo_fallback.frag.glsl").GetAwaiter().GetResult();
        using var fallback = renderer.CreateShaderModuleFromGlsl(
            fallbackGlsl,
            ShaderModuleStage.Fragment,
            "shader.Demo.Fallback.Frag",
            "mods/Cyberland.Demo/Content/Shaders/demo_fallback.frag.glsl");
    }
}
