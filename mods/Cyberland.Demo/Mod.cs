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
/// <see cref="ScenePath"/> into the root world via <see cref="ISceneRuntime.SpawnIntoWorldAsync"/>. Entity layout lives in
/// that JSON; follow each system under <c>Systems/</c> for per-frame behavior. <c>overlay.json</c> /
/// <c>room.json</c> are optional additive-world samples (<see cref="ISceneRuntime.BeginLoad"/>). Other Cyberland demos
/// increase gameplay scope (Snake, Pong, BrickBreaker) after you understand this pipeline.</para>
/// <para><b>Frame flow (simplified):</b>
/// <see cref="InputSystem"/> (early, parallel velocity clears/writes; sequential axis read between barriers) →
/// <see cref="IntegrateSystem"/> (fixed, <see cref="ISingletonSystem"/>) moves <see cref="Scene.Transform"/> and clamps to the virtual canvas →
/// <see cref="VelocityDampSystem"/> (fixed, parallel) scales velocity down so motion doesn’t feel frictionless →
/// lighting/post runs in engine/system order →
/// <see cref="PostVolumeFillSystem"/> (late, singleton) retargets the fullscreen bloom volume and gain →
/// <see cref="HudUiSystem"/> (late, singleton) updates retained HUD FPS text.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the HDR root-world scene document (see <see cref="SetupSceneAsync"/>).</summary>
    public const string ScenePath = "Scenes/hdr.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("hdr.json");
        KickoffBuiltinAtlasLoads(context);
        ValidateShaderModuleLoadPaths(context);

        var host = context.Host;

        var hud = await SetupSceneAsync(context);

        context.RegisterSingleton("cyberland.demo/post-volume", new PostVolumeFillSystem(host));
        context.RegisterParallel("cyberland.demo/input", new InputSystem(host, context.Scheduler));
        context.RegisterSingleton("cyberland.demo/integrate", new IntegrateSystem(host));
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());
        context.RegisterSingleton("cyberland.demo/hud-ui", new HudUiSystem(host, hud));
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
        var assets = new AssetManager(context.VirtualFileSystem);
        var renderer = context.Host.Renderer;

        var precompiledSpirv = assets.LoadBytes("Shaders/precompiled.vert.glsl.spv");
        using var precompiled = renderer.CreateShaderModuleFromSpirv(
            precompiledSpirv,
            "shader.Demo.Precompiled.Vert");

        var fallbackGlsl = assets.LoadTextAsync("Shaders/fallback.frag.glsl").GetAwaiter().GetResult();
        using var fallback = renderer.CreateShaderModuleFromGlsl(
            fallbackGlsl,
            ShaderModuleStage.Fragment,
            "shader.Demo.Fallback.Frag",
            "mods/Cyberland.Demo/Content/Shaders/fallback.frag.glsl");
    }

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap the HDR demo from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "HDR scene spawn failed.");

        if (context.Scenes is not null)
        {
            _ = context.Scenes.BeginLoad(new SceneLoadDescriptor
            {
                ScenePath = "Scenes/overlay.json",
                Priority = -100
            });
            _ = context.Scenes.BeginLoad(new SceneLoadDescriptor
            {
                ScenePath = "Scenes/room.json",
                Priority = 0
            });
        }

        return ResolveHudRefs(context);
    }
}
