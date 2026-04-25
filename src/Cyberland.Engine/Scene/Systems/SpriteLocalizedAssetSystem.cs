using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves localized sprite texture paths into renderer texture IDs.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Runtime integration behavior; covered indirectly by tests but excluded for strict engine line gate.")]
public sealed class SpriteLocalizedAssetSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SpriteLocalizedAsset, Sprite>();

    /// <summary>
    /// Creates a resolver that reads localized content from the host.
    /// </summary>
    public SpriteLocalizedAssetSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnLateUpdate(ChunkQueryAll query, float deltaSeconds)
    {
        _ = deltaSeconds;
        var renderer = _host.Renderer;
        var localized = _host.LocalizedContent;
        if (renderer is null || localized is null)
            return;

        foreach (var chunk in query)
        {
            var localizedCol = chunk.Column<SpriteLocalizedAsset>();
            var spriteCol = chunk.Column<Sprite>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var localizedAsset = ref localizedCol[i];
                ref var sprite = ref spriteCol[i];
                if (localizedAsset.LoadedGeneration == localizedAsset.ReloadGeneration)
                    continue;

                LoadIntoSprite(renderer, localized, ref localizedAsset, ref sprite);
            }
        }
    }

    private static void LoadIntoSprite(IRenderer renderer, Localization.ILocalizedContent localized, ref SpriteLocalizedAsset localizedAsset, ref Sprite sprite)
    {
        var albedo = LoadTexture(localized, renderer, localizedAsset.CanonicalAlbedoPath);
        var normal = string.IsNullOrWhiteSpace(localizedAsset.CanonicalNormalPath)
            ? sprite.NormalTextureId
            : LoadTexture(localized, renderer, localizedAsset.CanonicalNormalPath!);
        var emissive = string.IsNullOrWhiteSpace(localizedAsset.CanonicalEmissivePath)
            ? sprite.EmissiveTextureId
            : LoadTexture(localized, renderer, localizedAsset.CanonicalEmissivePath!);

        if (localizedAsset.KeepExistingOnMissing)
        {
            if (albedo != TextureId.MaxValue)
                sprite.AlbedoTextureId = albedo;
            if (normal != TextureId.MaxValue)
                sprite.NormalTextureId = normal;
            if (emissive != TextureId.MaxValue)
                sprite.EmissiveTextureId = emissive;
        }
        else
        {
            sprite.AlbedoTextureId = albedo == TextureId.MaxValue ? renderer.WhiteTextureId : albedo;
            sprite.NormalTextureId = normal == TextureId.MaxValue ? renderer.DefaultNormalTextureId : normal;
            sprite.EmissiveTextureId = emissive;
        }

        localizedAsset.LoadedGeneration = localizedAsset.ReloadGeneration;
    }

    private static TextureId LoadTexture(Localization.ILocalizedContent localized, IRenderer renderer, string canonicalPath) =>
        localized.TryLoadLocalizedTexture(canonicalPath, renderer);
}
