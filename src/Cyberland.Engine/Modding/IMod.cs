namespace Cyberland.Engine.Modding;

/// <summary>
/// Implement this in a mod DLL to run code when the host loads your mod. The shipped game uses the same contract as third-party mods.
/// </summary>
/// <remarks>
/// The host reads <c>manifest.json</c> (see <see cref="ModManifest"/>) from your mod folder, loads your assembly, finds a concrete <see cref="IMod"/>,
/// then calls <see cref="OnLoad"/> once. Register ECS systems, spawn entities, and mount extra content paths there; release resources in <see cref="OnUnload"/>.
/// </remarks>
/// <example>
/// <code lang="csharp">
/// public sealed class MyMod : IMod
/// {
///     public void OnLoad(ModLoadContext context)
///     {
///         context.RegisterSequential("my.game/tick", new MyTickSystem(context.Host));
///     }
///     public void OnUnload() { }
/// }
/// </code>
/// </example>
public interface IMod
{
    /// <summary>
    /// Called once after content mounts and host services (<see cref="Hosting.GameHostServices"/>) are ready.
    /// Register <see cref="Core.Tasks.SystemScheduler"/> entries: <see cref="Core.Ecs.ISystem"/> / <see cref="Core.Ecs.IParallelSystem"/> plus optional
    /// <see cref="Core.Ecs.IEarlyUpdate"/>, <see cref="Core.Ecs.IFixedUpdate"/>, <see cref="Core.Ecs.ILateUpdate"/> (or parallel equivalents).
    /// </summary>
    /// <param name="context">Access to the shared <see cref="Core.Ecs.World"/>, scheduler, VFS, and host.</param>
    void OnLoad(ModLoadContext context);

    /// <summary>Called when the game shuts down or reloads mods; free anything tied to this mod instance.</summary>
    void OnUnload();
}
