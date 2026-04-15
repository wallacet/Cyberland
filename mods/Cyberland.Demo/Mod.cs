using Cyberland.Engine;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo;

// Tutorial map — HDR sprite sample (loadOrder 10; often disabled in manifest for publish).
//
// Data flow: sequential early input -> velocity-damp (optional parallel fixed over Velocity chunks) & sequential fixed integrate
// -> sequential late HDR light/post fill (writes ECS sources; engine submits). Scene setup runs once in OnStart (sequential, no phase).
//
// Registration order (ids): scene-setup -> hdr stationary lights fill -> hdr player point -> hdr post volume -> input ->
// integrate -> velocity-damp(parallel).
public sealed class Mod : IMod
{
    public void OnLoad(ModLoadContext context)
    {
        context.MountDefaultContent();
        // Mod load path is synchronous; block on locale merge so string keys exist before systems start.
        context.LocalizedContent.MergeStringTableAsync("demo_hdr.json").GetAwaiter().GetResult();

        var host = context.Host;
        context.RegisterSequential("cyberland.demo/scene-setup", new SceneSetupSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-stationary-lights", new HdrStationaryLightsFillSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-player-point", new HdrPlayerPointLightFillSystem(host));
        context.RegisterSequential("cyberland.demo/hdr-post-volume", new HdrPostVolumeFillSystem(host));
        context.RegisterSequential("cyberland.demo/input", new InputSystem(host, context.Scheduler));
        context.RegisterSequential("cyberland.demo/integrate", new IntegrateSystem(host));
        context.RegisterParallel("cyberland.demo/velocity-damp", new VelocityDampSystem());
    }

    public void OnUnload()
    {
    }
}
