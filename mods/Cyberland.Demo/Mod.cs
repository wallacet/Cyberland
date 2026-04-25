using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo;

// Tutorial — HDR 2D sample (loadOrder 10; manifest may set disabled: true for publish). Progression: read this, then
// Cyberland.Demo.Snake, Pong, BrickBreaker in order of rising ECS scope.
//
// Data flow: sequential early input -> fixed integrate (move) -> fixed velocity-damp (scale down velocity) ->
// late HDR light/post. Integrate first then damp: each step applies motion, then framerate-aware decay on Velocity.
// Scene setup runs in OnStart only (sequential, no phase).
//
// Registration: scene-setup -> hdr fills -> input -> integrate -> tag-query (parallel, teaches chunk query) ->
// velocity-damp -> fps-display (late: moving-average FPS on HudFpsTag).
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        DemoInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("demo_hdr.json");

        var host = context.Host;
        context.RegisterSequential("cyberland.demo/scene-setup", new SceneSetupSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-stationary-lights", new HdrStationaryLightsFillSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-player-point", new HdrPlayerPointLightFillSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-post-volume", new HdrPostVolumeFillSystem(host));
        context.RegisterSequential("cyberland.demo/input", new InputSystem(host, context.Scheduler));
        context.RegisterSequential("cyberland.demo/integrate", new IntegrateSystem(host));
        context.RegisterParallel("cyberland.demo/tag-query-showcase", new TagQueryShowcaseSystem());
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());
        context.RegisterSequential("cyberland.demo/fps-display", new FpsDisplaySystem(host));
    }

    public void OnUnload()
    {
    }
}
