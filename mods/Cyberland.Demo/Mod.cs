using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo;

/// <summary>
/// HDR lighting + post-process tutorial mod: spawns a small 2D scene, drives the player with ECS systems, and shows how
/// sequential vs parallel registration fits a frame (early → fixed → late; parallel fixed uses host <see cref="ParallelOptions"/>).
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> this file lists registration order. Follow each system’s source for phase callbacks;
/// <see cref="SceneSetupSystem"/> is intentionally large—it is the static scene “recipe.” Other Cyberland demos increase
/// gameplay scope (Snake, Pong, BrickBreaker) after you understand this pipeline.</para>
/// <para><b>Frame flow (simplified):</b>
/// <see cref="InputSystem"/> (early, parallel velocity clears/writes; sequential axis read between barriers) →
/// <see cref="IntegrateSystem"/> (fixed, sequential) moves <see cref="Scene.Transform"/> and clamps to the virtual canvas →
/// <see cref="VelocityDampSystem"/> (fixed, parallel) scales velocity down so motion doesn’t feel frictionless →
/// lighting/post runs in engine/system order →
/// <see cref="HdrPostVolumeFillSystem"/> (late) retargets the fullscreen bloom volume and gain →
/// <see cref="FpsDisplaySystem"/> (late) updates HUD text.</para>
/// <para><b>Registration order matters</b> where systems depend on published state: integrate must see input’s velocity,
/// damp runs after integrate in the same fixed phase, and post-volume reads the player transform after hierarchy/lighting
/// have advanced for the tick.</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        DemoInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("demo_hdr.json");

        var host = context.Host;

        // One-shot entity creation + HDR rig. Sequential, OnStart only — see SceneSetupSystem remarks.
        context.RegisterSerial("cyberland.demo/scene-setup", new SceneSetupSystem(host));

        // Late: keeps the authored bloom volume centered on the swapchain viewport and ties bloom strength to player X.
        context.RegisterSerial("cyberland.demo/hdr-post-volume", new HdrPostVolumeFillSystem(host));

        // Early: parallel velocity SoA updates; axis read runs on the scheduler thread between Parallel.ForEach barriers.
        context.RegisterParallel("cyberland.demo/input", new InputSystem(host, context.Scheduler));
        context.RegisterSerial("cyberland.demo/integrate", new IntegrateSystem(host));

        // Parallel chunk pass example (same fixed phase as integrate; host supplies ParallelOptions for partitioning).
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());

        // Late: HUD overlay; uses presentation viewport size so text stays in the corner when the window scales.
        context.RegisterSerial("cyberland.demo/fps-display", new FpsDisplaySystem(host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
