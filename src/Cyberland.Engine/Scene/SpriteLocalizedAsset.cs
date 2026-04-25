using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Rendering;

namespace Cyberland.Engine.Scene;

/// <summary>
/// Localized sprite texture binding descriptor resolved by <see cref="Systems.SpriteLocalizedAssetSystem"/>.
/// </summary>
/// <remarks>
/// Set <see cref="CanonicalAlbedoPath"/> to a mod-local content path such as
/// <c>Textures/Pickups/shard.png</c>. The resolver probes locale overlays first via
/// <see cref="Localization.ILocalizedContent"/> and writes the loaded texture into <see cref="Sprite.AlbedoTextureId"/>.
/// </remarks>
[RequiresComponent<Sprite>]
public struct SpriteLocalizedAsset : IComponent
{
    /// <summary>
    /// Canonical albedo texture path inside mod content.
    /// </summary>
    public string CanonicalAlbedoPath;

    /// <summary>
    /// Optional canonical normal-map path inside mod content.
    /// </summary>
    public string? CanonicalNormalPath;

    /// <summary>
    /// Optional canonical emissive path inside mod content.
    /// </summary>
    public string? CanonicalEmissivePath;

    /// <summary>
    /// Generation value incremented by gameplay code to request a reload.
    /// </summary>
    public int ReloadGeneration;

    /// <summary>
    /// Last generation that has been resolved and uploaded.
    /// </summary>
    public int LoadedGeneration;

    /// <summary>
    /// Whether to preserve existing sprite texture IDs when loading fails.
    /// </summary>
    public bool KeepExistingOnMissing;
}
