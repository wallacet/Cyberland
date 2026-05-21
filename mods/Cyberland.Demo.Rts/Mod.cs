using Cyberland.Demo.Rts.Systems;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.Rts;

/// <summary>RTS-style tutorial: pan/zoom camera, ~10 units, control groups, box select, formation moves with separation, FPS HUD.</summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/>; <see cref="Mod.Playfield"/> wires checkerboard + session; systems under <c>Systems/</c>.</para>
/// <para><b>Frame flow (simplified):</b> <see cref="InputSystem"/> (groups, selection, orders) → <see cref="UnitMoveSystem"/> (serial) → <see cref="CameraSystem"/> (focus + pan/zoom) → <see cref="SelectionFrameSystem"/> → <see cref="HudUiSystem"/>.</para>
/// <para><b>MSDF:</b> mono atlas is kicked async (fire-and-forget) for the FPS row; first frames may briefly fall back until upload drains.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    public const int ViewportWidth = 1280;
    public const int ViewportHeight = 720;

    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/rts.json";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        InputSetup.RegisterDefaultBindings(context);
        _ = context.LoadBakedMsdfAtlasAsync(BuiltinFonts.BakedAtlasManifestPath.MonoRegular14);
        var hud = await SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.rts/input", new InputSystem(host));
        context.RegisterSerial("cyberland.demo.rts/unit-move", new UnitMoveSystem());
        context.RegisterSingleton("cyberland.demo.rts/camera", new CameraSystem(host));
        context.RegisterSingleton("cyberland.demo.rts/selection", new SelectionFrameSystem());
        context.RegisterSingleton("cyberland.demo.rts/hud-ui", new HudUiSystem(host, hud));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask<HudDocumentRefs> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap RTS from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "RTS scene spawn failed.");

        WirePlayfieldAfterSpawn(context);
        return ResolveHudRefs(context);
    }
}
