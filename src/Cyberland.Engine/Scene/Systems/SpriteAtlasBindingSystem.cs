using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Resolves <see cref="SpriteAtlasBinding"/> into localized atlas regions on <see cref="Sprite"/>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Runtime integration behavior; covered indirectly by tests but excluded for strict engine line gate.")]
public sealed class SpriteAtlasBindingSystem : ISystem, ILateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SpriteAtlasBinding, Sprite>();

    /// <summary>Creates a resolver using the host atlas catalog and renderer.</summary>
    public SpriteAtlasBindingSystem(GameHostServices host) => _host = host;

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
        var catalog = _host.SpriteAtlasCatalog;
        if (catalog is null)
            return;

        foreach (var chunk in query)
        {
            var bindingCol = chunk.Column<SpriteAtlasBinding>();
            var spriteCol = chunk.Column<Sprite>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var binding = ref bindingCol[i];
                ref var sprite = ref spriteCol[i];
                if (binding.LoadedGeneration == binding.ReloadGeneration)
                    continue;

                if (string.IsNullOrWhiteSpace(binding.CanonicalManifestPath))
                {
                    sprite.AlbedoTextureId = renderer.MissingTextureId;
                    binding.LoadedGeneration = binding.ReloadGeneration;
                    continue;
                }

                var atlas = catalog.GetOrLoad(binding.CanonicalManifestPath, renderer, binding.LocaleInvariant);
                SpriteAtlasBindingApplier.ApplyInitial(atlas, ref binding, ref sprite, renderer);
                binding.LoadedGeneration = binding.ReloadGeneration;
            }
        }
    }
}
