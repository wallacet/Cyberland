namespace Cyberland.Engine.Modding;

/// <summary>
/// Implement this in a mod DLL to run code when the host loads your mod. The shipped game uses the same contract as third-party mods.
/// </summary>
/// <remarks>
/// <para>
/// The host reads <c>manifest.json</c> (see <see cref="ModManifest"/>) from your mod folder, loads your assembly, finds a concrete <see cref="IMod"/>,
/// then calls <see cref="OnLoadAsync"/> once. Register ECS systems, spawn entities, and mount extra content paths there; release resources in <see cref="OnUnload"/>.
/// </para>
/// <para>
/// <strong>Baked MSDF atlases:</strong> do not <c>await</c> <see cref="ModLoadContext.LoadBakedMsdfAtlasAsync"/> from <see cref="OnLoadAsync"/> while the host is still inside
/// <see cref="ModLoader.LoadAll"/> (see that method’s remarks). Use fire-and-forget, the synchronous <see cref="ModLoadContext.LoadBakedMsdfAtlas"/>, or defer to post-startup code.
/// </para>
/// <para>
/// <strong>Cold-start scene convention:</strong> put one-off entity authoring (camera, playfield, HUD, lights, global post) in a static helper
/// (e.g. <c>SceneSetup.SetupSceneAsync</c>) and await it from <see cref="OnLoadAsync"/> before
/// <see cref="ModLoadContext.RegisterSerial"/> / <see cref="ModLoadContext.RegisterParallel"/> / <see cref="ModLoadContext.RegisterSingleton"/>.
/// Project docs describe this pattern under mod authoring rules.
/// </para>
/// </remarks>
/// <example>
/// <code lang="csharp">
/// public sealed class MyMod : IMod
/// {
///     public ValueTask OnLoadAsync(ModLoadContext context)
///     {
///         context.RegisterSerial("my.game/tick", new MyTickSystem(context.Host));
///         return ValueTask.CompletedTask;
///     }
///     public void OnUnload() { }
/// }
/// </code>
/// </example>
public interface IMod
{
    /// <summary>
    /// Called once after content mounts and host services (<see cref="Hosting.GameHostServices"/>) are ready.
    /// The host may choose to present one or more bootstrap frames before invoking mod load; rely on this callback
    /// (not first-present timing) as the start of gameplay/system registration.
    /// Register <see cref="Core.Tasks.SystemScheduler"/> entries: <see cref="Core.Ecs.ISystem"/> / <see cref="Core.Ecs.IParallelSystem"/> (declare chunk needs via <see cref="Core.Ecs.IEcsQuerySource.QuerySpec"/>)
    /// plus optional <see cref="Core.Ecs.IEarlyUpdate"/>, <see cref="Core.Ecs.IFixedUpdate"/>, <see cref="Core.Ecs.ILateUpdate"/> (or parallel equivalents).
    /// </summary>
    /// <param name="context">Access to the shared <see cref="Core.Ecs.World"/>, scheduler, VFS, and host.</param>
    ValueTask OnLoadAsync(ModLoadContext context);

    /// <summary>Called when the game shuts down or reloads mods; free anything tied to this mod instance.</summary>
    void OnUnload();
}
