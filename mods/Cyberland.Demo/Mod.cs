using Cyberland.Engine;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo;

/// <summary>
/// HDR lighting + post-process tutorial mod: loads a small 2D scene from JSON, drives the player with ECS systems, and shows how
/// sequential vs parallel registration fits a frame (early → fixed → late; parallel fixed uses host <see cref="ParallelOptions"/>).
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> this file lists bootstrap and registration order. Cold start is
/// <see cref="SetupSceneAsync"/> (private): registers <see cref="SceneComponentDeserializers"/> and spawns
/// <see cref="HdrScenePath"/> into the root world via <see cref="ISceneRuntime.SpawnIntoWorldAsync"/>. Entity layout lives in
/// that JSON; follow each system under <c>Systems/</c> for per-frame behavior. <c>demo_overlay.json</c> /
/// <c>demo_room.json</c> are optional additive-world samples (<see cref="ISceneRuntime.BeginLoad"/>). Other Cyberland demos
/// increase gameplay scope (Snake, Pong, BrickBreaker) after you understand this pipeline.</para>
/// <para><b>Frame flow (simplified):</b>
/// <see cref="InputSystem"/> (early, parallel velocity clears/writes; sequential axis read between barriers) →
/// <see cref="IntegrateSystem"/> (fixed, <see cref="ISingletonSystem"/>) moves <see cref="Scene.Transform"/> and clamps to the virtual canvas →
/// <see cref="VelocityDampSystem"/> (fixed, parallel) scales velocity down so motion doesn’t feel frictionless →
/// lighting/post runs in engine/system order →
/// <see cref="HdrPostVolumeFillSystem"/> (late, singleton) retargets the fullscreen bloom volume and gain →
/// <see cref="FpsDisplaySystem"/> (late, singleton) updates HUD text.</para>
/// <para><b>Registration order matters</b> where systems depend on published state: integrate must see input’s velocity,
/// damp runs after integrate in the same fixed phase, and post-volume reads the player transform after hierarchy/lighting
/// have advanced for the tick. <see cref="SetupSceneAsync"/> runs before any <c>Register*</c> so <c>OnStart</c> /
/// <c>OnSingletonStart</c> see a complete world.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <summary>VFS path to the HDR root-world scene document (see <see cref="SetupSceneAsync"/>).</summary>
    public const string HdrScenePath = "Scenes/demo_hdr.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("demo_hdr.json");
        KickoffBuiltinAtlasLoads(context);
        ValidateShaderModuleLoadPaths(context);

        var host = context.Host;

        await SetupSceneAsync(context);

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

    /// <summary>
    /// Loads <see cref="HdrScenePath"/> into <see cref="ModLoadContext.World"/> (root session pair).
    /// </summary>
    /// <remarks>
    /// <para>Registers mod scene component types, then <see cref="ISceneRuntime.SpawnIntoWorldAsync"/> parses and spawns
    /// the JSON in one shot. Layout, lights, HUD rows, and post tuning are authored in
    /// <c>Content/Scenes/demo_hdr.json</c>—not in C# spawn helpers.</para>
    /// <para>Per-frame work stays in registered systems: player placement (<see cref="IntegrateSystem.OnSingletonStart"/>),
    /// bloom volume follow (<see cref="HdrPostVolumeFillSystem"/>), FPS text (<see cref="FpsDisplaySystem"/>).</para>
    /// </remarks>
    private static async ValueTask SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap the HDR demo from JSON.");

        SceneComponentDeserializers.Register(context.Scenes, context.Host);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            HdrScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "HDR scene spawn failed.");

        // Additive scene samples: host pumps BeginLoad on the render tick (never block ModLoader on GPU drains).
        if (context.Scenes is not null)
        {
            _ = context.Scenes.BeginLoad(new SceneLoadDescriptor
            {
                ScenePath = "Scenes/demo_overlay.json",
                Priority = -100
            });
            _ = context.Scenes.BeginLoad(new SceneLoadDescriptor
            {
                ScenePath = "Scenes/demo_room.json",
                Priority = 0
            });
        }
    }
}
