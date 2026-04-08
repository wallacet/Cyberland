namespace Cyberland.Engine.Modding;

/// <summary>
/// Optional entry point for code mods. The base game uses the same contract as third-party mods.
/// </summary>
public interface IMod
{
    ModManifest Manifest { get; }

    void OnLoad(ModLoadContext context);
    void OnUnload();
}
