namespace Cyberland.Engine.Modding;

/// <summary>
/// Optional entry point for code mods. The base game uses the same contract as third-party mods.
/// Mod metadata (<see cref="ModManifest"/>) is read from each mod folder's <c>manifest.json</c> by <see cref="ModLoader"/> and exposed on <see cref="ModLoadContext.Manifest"/> during <see cref="OnLoad"/>.
/// </summary>
public interface IMod
{
    void OnLoad(ModLoadContext context);
    void OnUnload();
}
