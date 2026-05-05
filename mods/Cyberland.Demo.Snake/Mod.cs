using Cyberland.Engine.Hosting;
using Cyberland.Engine.Modding;

namespace Cyberland.Demo.Snake;

/// <summary>
/// Snake sample: Control + Session + Tilemap + VisualBundle; simulation lives in <see cref="Session.Step"/>.
/// </summary>
/// <remarks>
/// <para><b>Where to read next:</b> <see cref="SceneSetup.SetupSceneAsync"/> for cold start; this file is scheduler registration only.</para>
/// <para>Gameplay systems use <see cref="ISingletonSystem"/> for single-row archetypes (session, control, tilemap, visuals — see individual <c>QuerySpec</c>s).</para>
/// </remarks>
public sealed class Mod : IMod
{
    /// <inheritdoc />
    public async ValueTask OnLoadAsync(ModLoadContext context)
    {
        context.MountDefaultContent();
        SnakeInputSetup.RegisterDefaultBindings(context);
        context.LocalizedContent.MergeStringTable("snake.json");

        await SceneSetup.SetupSceneAsync(context);

        var host = context.Host;
        context.RegisterSingleton("cyberland.demo.snake/bootstrap", new BootstrapSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/input", new InputSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/tick", new TickSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/tilemap-layout", new TilemapLayoutSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/lights", new SnakeLightsFillSystem(host));
        context.RegisterSingleton("cyberland.demo.snake/visual-sync", new VisualSyncSystem(host));
    }

    /// <inheritdoc />
    public void OnUnload() { }
}
