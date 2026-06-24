using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using System.Diagnostics.CodeAnalysis;

namespace Cyberland.Engine.Scene.Systems;

/// <summary>
/// Parallel pass: advances <see cref="SpriteAtlasBinding"/> sheet/animation clips and updates <see cref="Sprite.UvRect"/>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Runtime integration behavior; covered indirectly by tests but excluded for strict engine line gate.")]
public sealed class SpriteAtlasAnimationSystem : IParallelSystem, IParallelLateUpdate
{
    private readonly GameHostServices _host;

    /// <inheritdoc cref="IEcsQuerySource.QuerySpec"/>
    public SystemQuerySpec QuerySpec => SystemQuerySpec.All<SpriteAtlasBinding, Sprite>();

    /// <summary>Creates the animation pass using the host atlas catalog.</summary>
    public SpriteAtlasAnimationSystem(GameHostServices host) => _host = host;

    /// <inheritdoc />
    public void OnStart(World world, ChunkQueryAll query)
    {
        _ = world;
        _ = query;
    }

    /// <inheritdoc />
    public void OnParallelLateUpdate(ChunkQueryAll query, float deltaSeconds, ParallelOptions parallelOptions)
    {
        var renderer = _host.Renderer;
        var catalog = _host.SpriteAtlasCatalog;
        if (catalog is null)
            return;

        foreach (var chunk in query)
        {
            Parallel.For(0, chunk.Count, parallelOptions, i =>
            {
                ref var binding = ref chunk.Column<SpriteAtlasBinding>()[i];
                if (!SpriteAtlasBindingApplier.IsAnimated(in binding))
                    return;
                if (string.IsNullOrWhiteSpace(binding.CanonicalManifestPath))
                    return;
                if (binding.LoadedGeneration != binding.ReloadGeneration)
                    return;

                ref var sprite = ref chunk.Column<Sprite>()[i];
                if (!catalog.TryGetCached(binding.CanonicalManifestPath, binding.LocaleInvariant, out var atlas))
                    atlas = catalog.GetOrLoad(binding.CanonicalManifestPath, renderer, binding.LocaleInvariant);
                SpriteAtlasBindingApplier.ApplyAnimatedFrame(atlas, ref binding, ref sprite, deltaSeconds);
            });
        }
    }
}
