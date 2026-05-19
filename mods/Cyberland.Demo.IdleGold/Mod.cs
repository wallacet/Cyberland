using Cyberland.Demo.IdleGold.Components;
using Cyberland.Demo.IdleGold.Systems;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Modding;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.RuntimeScenes;

namespace Cyberland.Demo.IdleGold;

/// <summary>
/// Idle gold UI showcase: passive income, purchases through retained UI, ECS singleton session row.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> private <see cref="SetupSceneAsync"/> spawns <see cref="ScenePath"/> and <c>Content/Ui/idlegold_hud.json</c>; <see cref="UiCommandHandler"/> for commands.</para>
/// <para><b>Frame flow:</b> UI buttons enqueue into <see cref="Cyberland.Engine.Hosting.GameHostServices.UiCommands"/>; <see cref="SimulationSystem"/> advances economy in late update; <see cref="HudBindSystem"/> mirrors state into labels.</para>
/// <para><b>MSDF bootstrap:</b> synchronous <see cref="LoadBuiltinUiAtlasesForIdleGold"/> so first UI frames use baked glyphs — see **cyberland-demo-mod-authoring**.</para>
/// </remarks>
public sealed partial class Mod : IMod
{
    /// <summary>VFS path to the root-world scene document.</summary>
    public const string ScenePath = "Scenes/demo_idlegold.json";

    public const string NavGather = "gather";
    public const string NavCharacter = "character";
    public const string NavBlacksmith = "blacksmith";
    public const string NavLog = "log";

    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        context.LocalizedContent.MergeStringTable("idlegold.json");
        LoadBuiltinUiAtlasesForIdleGold(context);

        var boot = await SetupSceneAsync(context);

        var host = context.Host;
        host.UiCommandDispatcher = cmd =>
            UiCommandHandler.Dispatch(context.World, boot.SessionEntity, context.LocalizedContent.Strings, cmd);

        context.RegisterSingleton("cyberland.demo.idlegold/simulation",
            new SimulationSystem(context.LocalizedContent.Strings));
        context.RegisterSingleton("cyberland.demo.idlegold/hud-bind",
            new HudBindSystem(boot.Refs, context.LocalizedContent.Strings, host));
    }

    /// <inheritdoc />
    public void OnUnload()
    {
    }

    private static async ValueTask<SceneBootstrap> SetupSceneAsync(ModLoadContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Scenes is null)
            throw new InvalidOperationException("Runtime scenes are required to bootstrap IdleGold from JSON.");

        SceneComponentDeserializers.Register(context.Scenes);

        var result = await context.Scenes.SpawnIntoWorldAsync(
            context.World,
            ScenePath,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            throw new InvalidOperationException(result.ErrorMessage ?? "IdleGold scene spawn failed.");

        var world = context.World;
        var session = world.RequireSingleEntityWith<SessionTag>("IdleGold session");
        WireSession(world, session, context.LocalizedContent.Strings);
        return BootstrapUi(context, session);
    }

    private static void WireSession(Cyberland.Engine.Core.Ecs.World world, Cyberland.Engine.Core.Ecs.EntityId session, LocalizationManager loc)
    {
        world.GetOrAdd<Wallet>(session) = new Wallet();
        world.GetOrAdd<Sources>(session) = new Sources
        {
            VillageBeg = new SourceRow { Unlocked = true, Level = 1 },
            ForestForage = default,
            CaveExplore = default,
            RoadToll = default
        };
        world.GetOrAdd<Stats>(session) = new Stats();
        world.GetOrAdd<Equipment>(session) = new Equipment();
        world.GetOrAdd<RngState>(session) = new RngState { State = 0xC0FFEE_DEAD_BEEFL };
        world.GetOrAdd<EventLog>(session) = new EventLog { Lines = new List<string>() };

        LogBook.Append(world, session, loc.Get("idlegold.log.welcome"));
    }

    /// <summary>
    /// Seeds the host text glyph cache from engine virtual paths (VFS miss → embedded builtin manifests).
    /// IdleGold does not ship duplicate PNGs under <c>Content/</c>; optional mod overrides can replace these paths.
    /// </summary>
    private static void LoadBuiltinUiAtlasesForIdleGold(ModLoadContext context)
    {
        ReadOnlySpan<string> manifests =
        [
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular13,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold14,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold18,
            BuiltinFonts.BakedAtlasManifestPath.UiSansBold23,
            BuiltinFonts.BakedAtlasManifestPath.UiSansRegular22,
            BuiltinFonts.BakedAtlasManifestPath.MonoRegular14
        ];

        foreach (var path in manifests)
            context.LoadBakedMsdfAtlas(path);
    }
}
