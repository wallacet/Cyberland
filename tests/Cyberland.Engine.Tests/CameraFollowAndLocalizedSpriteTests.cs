using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;
using TextureId = System.UInt32;

namespace Cyberland.Engine.Tests;

public sealed class CameraFollowAndLocalizedSpriteTests
{
    [Fact]
    public void CameraFollowSystem_moves_camera_toward_target_and_clamps()
    {
        var world = new World();
        var target = world.CreateEntity();
        world.GetOrAdd<Transform>(target) = new Transform
        {
            LocalPosition = new Vector2D<float>(900f, 900f),
            WorldPosition = new Vector2D<float>(900f, 900f)
        };

        var camera = world.CreateEntity();
        world.GetOrAdd<Transform>(camera) = Transform.Identity;
        world.GetOrAdd<Camera2D>(camera) = Camera2D.Create(new Vector2D<int>(1280, 720));
        world.GetOrAdd<CameraFollow2D>(camera) = new CameraFollow2D
        {
            Enabled = true,
            Target = target,
            OffsetWorld = new Vector2D<float>(100f, 0f),
            FollowLerp = 1f,
            ClampToBounds = true,
            BoundsMinWorld = new Vector2D<float>(100f, 100f),
            BoundsMaxWorld = new Vector2D<float>(640f, 360f)
        };

        var system = new CameraFollowSystem();
        var query = world.QueryChunks(SystemQuerySpec.All<CameraFollow2D, Transform>());
        system.OnStart(world, query);
        system.OnParallelFixedUpdate(query, 1f / 60f, new ParallelOptions { MaxDegreeOfParallelism = 1 });

        ref readonly var transform = ref world.Get<Transform>(camera);
        Assert.Equal(new Vector2D<float>(640f, 360f), transform.WorldPosition);
    }

    [Fact]
    public void CameraFollowSystem_skips_disabled_or_missing_target()
    {
        var world = new World();
        var camera = world.CreateEntity();
        world.GetOrAdd<Transform>(camera) = new Transform
        {
            LocalPosition = new Vector2D<float>(20f, 30f),
            WorldPosition = new Vector2D<float>(20f, 30f)
        };
        world.GetOrAdd<Camera2D>(camera) = Camera2D.Create(new Vector2D<int>(1280, 720));
        world.GetOrAdd<CameraFollow2D>(camera) = new CameraFollow2D
        {
            Enabled = false,
            Target = new EntityId(999),
            FollowLerp = 1f
        };

        var system = new CameraFollowSystem();
        var query = world.QueryChunks(SystemQuerySpec.All<CameraFollow2D, Transform>());
        system.OnStart(world, query);
        system.OnParallelFixedUpdate(query, 1f / 60f, new ParallelOptions { MaxDegreeOfParallelism = 1 });

        ref readonly var transform = ref world.Get<Transform>(camera);
        Assert.Equal(new Vector2D<float>(20f, 30f), transform.WorldPosition);
    }

    [Fact]
    public void SpriteLocalizedAssetSystem_loads_texture_when_generation_changes()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices
        {
            Renderer = renderer,
            LocalizedContent = new FakeLocalizedContent(renderer, 77)
        };

        var world = new World();
        var spriteEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(spriteEntity) = Transform.Identity;
        world.GetOrAdd<Sprite>(spriteEntity) =
            Sprite.DefaultWhiteUnlit(renderer.WhiteTextureId, renderer.DefaultNormalTextureId, new Vector2D<float>(8f, 8f));
        world.GetOrAdd<SpriteLocalizedAsset>(spriteEntity) = new SpriteLocalizedAsset
        {
            CanonicalAlbedoPath = "Textures/Pickups/shard.png",
            ReloadGeneration = 1,
            LoadedGeneration = 0,
            KeepExistingOnMissing = false
        };

        var system = new SpriteLocalizedAssetSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<SpriteLocalizedAsset, Sprite>());
        system.OnStart(world, query);
        system.OnLateUpdate(query, 0f);

        ref readonly var sprite = ref world.Get<Sprite>(spriteEntity);
        ref readonly var localizedAsset = ref world.Get<SpriteLocalizedAsset>(spriteEntity);
        Assert.Equal((TextureId)77, sprite.AlbedoTextureId);
        Assert.Equal(1, localizedAsset.LoadedGeneration);
    }

    [Fact]
    public void SpriteLocalizedAssetSystem_keep_existing_path_preserves_missing_textures()
    {
        var renderer = new RecordingRenderer();
        var host = new GameHostServices
        {
            Renderer = renderer,
            LocalizedContent = new FakeLocalizedContent(renderer, TextureId.MaxValue)
        };

        var world = new World();
        var spriteEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(spriteEntity) = Transform.Identity;
        world.GetOrAdd<Sprite>(spriteEntity) =
            Sprite.DefaultWhiteUnlit(renderer.WhiteTextureId, renderer.DefaultNormalTextureId, new Vector2D<float>(8f, 8f));
        world.GetOrAdd<SpriteLocalizedAsset>(spriteEntity) = new SpriteLocalizedAsset
        {
            CanonicalAlbedoPath = "Textures/Pickups/shard.png",
            ReloadGeneration = 1,
            LoadedGeneration = 0,
            KeepExistingOnMissing = true
        };

        var system = new SpriteLocalizedAssetSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<SpriteLocalizedAsset, Sprite>());
        system.OnStart(world, query);
        system.OnLateUpdate(query, 0f);

        ref readonly var sprite = ref world.Get<Sprite>(spriteEntity);
        Assert.Equal(renderer.WhiteTextureId, sprite.AlbedoTextureId);
    }

    [Fact]
    public void SpriteLocalizedAssetSystem_noop_when_host_services_missing()
    {
        var host = new GameHostServices();
        var world = new World();
        var spriteEntity = world.CreateEntity();
        world.GetOrAdd<Transform>(spriteEntity) = Transform.Identity;
        world.GetOrAdd<Sprite>(spriteEntity) = default;
        world.GetOrAdd<SpriteLocalizedAsset>(spriteEntity) = new SpriteLocalizedAsset
        {
            CanonicalAlbedoPath = "Textures/Pickups/shard.png",
            ReloadGeneration = 3,
            LoadedGeneration = 0
        };

        var system = new SpriteLocalizedAssetSystem(host);
        var query = world.QueryChunks(SystemQuerySpec.All<SpriteLocalizedAsset, Sprite>());
        system.OnStart(world, query);
        system.OnLateUpdate(query, 0f);

        ref readonly var localized = ref world.Get<SpriteLocalizedAsset>(spriteEntity);
        Assert.Equal(0, localized.LoadedGeneration);
    }

    private sealed class FakeLocalizedContent : ILocalizedContent
    {
        private readonly IRenderer _renderer;
        private readonly TextureId _textureId;

        public FakeLocalizedContent(IRenderer renderer, TextureId textureId)
        {
            _renderer = renderer;
            _textureId = textureId;
            Strings = new LocalizationManager();
        }

        public string PrimaryCultureName => "en";
        public LocalizationManager Strings { get; }
        public Task MergeStringTableAsync(string tableFileName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void MergeStringTable(string tableFileName) { }
        public string? TryResolveLocalizedPath(string canonicalContentPath) => canonicalContentPath;
        public Task<byte[]?> TryLoadLocalizedBytesAsync(string canonicalContentPath, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
        public Task<TextureId> TryLoadLocalizedTextureAsync(string canonicalContentPath, IRenderer renderer,
            CancellationToken cancellationToken = default)
        {
            Assert.Same(_renderer, renderer);
            return Task.FromResult(_textureId);
        }

        public TextureId TryLoadLocalizedTexture(string canonicalContentPath, IRenderer renderer)
        {
            Assert.Same(_renderer, renderer);
            return _textureId;
        }

        public Stream? TryOpenLocalizedRead(string canonicalContentPath) => null;
    }
}
