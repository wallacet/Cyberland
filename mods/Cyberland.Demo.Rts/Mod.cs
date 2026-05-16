using Cyberland.Demo.Rts.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;

namespace Cyberland.Demo.Rts;

/// <summary>RTS-style tutorial: pan/zoom camera, one selectable unit with move orders, deferred lights, selection frame, FPS HUD.</summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for every entity and texture registration, then each system under <c>Systems/</c>.</para>
/// <para><b>Frame flow (simplified):</b> <see cref="RtsInputSystem"/> reads pan/zoom and queues click-to-move → <see cref="RtsUnitMoveSystem"/> integrates toward the target → <see cref="RtsCameraSystem"/> keeps the virtual viewport aligned with zoom state → <see cref="RtsSelectionFrameSystem"/> arranges the green bar sprites around the unit when selected → <see cref="RtsFpsHudSystem"/> updates the corner FPS row.</para>
/// <para><b>Registration order:</b> input before move so the session row’s move target flags are written before integration; camera after move so following sees the latest unit position; selection after camera so bars use the same camera-relative framing assumptions as the rest of the late pass.</para>
/// <para><b>MSDF:</b> mono atlas is kicked async (fire-and-forget) for the FPS row; first frames may briefly fall back until upload drains.</para>
/// </remarks>
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
