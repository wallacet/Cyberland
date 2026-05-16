using Cyberland.Demo.Rts.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo.Rts;

/// <summary>Optional RTS-style sample: panning camera, single unit, move orders, deferred lighting, viewport FPS HUD.</summary>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        RtsInputSetup.RegisterDefaultBindings(context);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
        await SceneSetup.SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.rts/input", new RtsInputSystem(host));
        context.RegisterSingleton("cyberland.demo.rts/unit-move", new RtsUnitMoveSystem());
        context.RegisterSingleton("cyberland.demo.rts/camera", new RtsCameraSystem(host));
        context.RegisterSingleton("cyberland.demo.rts/selection", new RtsSelectionFrameSystem());
        context.RegisterSingleton("cyberland.demo.rts/fps-hud", new RtsFpsHudSystem(host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }
}
