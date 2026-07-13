using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Audio;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Serialization;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.Tests;

public sealed class SceneRuntimeEngineDeserializerTests
{
    [Fact]
    public void LogicalActorLookup_ResolveAndTryResolve()
    {
        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<LogicalActorId>(e).Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

        Assert.True(LogicalActorLookup.TryResolve(world, guid, out var found));
        Assert.Equal(e, found);
        Assert.Equal(e, LogicalActorLookup.Resolve(world, guid));

        Assert.False(LogicalActorLookup.TryResolve(world, "00000000-0000-0000-0000-000000000099", out _));
        Assert.Throws<InvalidOperationException>(() => LogicalActorLookup.Resolve(world, "missing-guid"));
        Assert.Throws<ArgumentNullException>(() => LogicalActorLookup.TryResolve(null!, guid, out _));
        Assert.Throws<ArgumentException>(() => LogicalActorLookup.TryResolve(world, " ", out _));
    }

    [Fact]
    public void RuntimeJsonReaders_Readers_cover_defaults_and_vectors()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "f": 1.5,
              "i": 3,
              "b": true,
              "s": "WorldSpace",
              "v2": { "x": 2, "y": 3 },
              "v3": { "x": 1, "y": 2, "z": 3 },
              "v4": { "x": 0.1, "y": 0.2, "z": 0.3, "w": 0.4 }
            }
            """);
        var data = doc.RootElement;
        Assert.Equal(1.5f, RuntimeJsonReaders.ReadFloat(data, "f", 0f));
        Assert.Equal(9f, RuntimeJsonReaders.ReadFloat(data, "missing", 9f));
        Assert.Equal(3, RuntimeJsonReaders.ReadInt(data, "i", 0));
        Assert.True(RuntimeJsonReaders.ReadBool(data, "b", false));
        Assert.Equal("WorldSpace", RuntimeJsonReaders.ReadString(data, "s"));
        Assert.Equal(CoordinateSpace.WorldSpace, RuntimeJsonReaders.ReadEnum(data, "s", CoordinateSpace.ViewportSpace));
        Assert.True(RuntimeJsonReaders.TryReadVec2(data, "v2", out var v2));
        Assert.Equal(2f, v2.X);
        Assert.True(RuntimeJsonReaders.TryReadVec3(data, "v3", out var v3));
        Assert.Equal(3f, v3.Z);
        Assert.True(RuntimeJsonReaders.TryReadVec4(data, "v4", out var v4));
        Assert.Equal(0.4f, v4.W);
        Assert.False(RuntimeJsonReaders.TryReadVec2(data, "nope", out _));
    }

    [Fact]
    public void RuntimeJsonReaders_ResolveFontFamilyId_maps_builtin_shorthand()
    {
        Assert.Equal(BuiltinFonts.UiSans, RuntimeJsonReaders.ResolveFontFamilyId(null));
        Assert.Equal(BuiltinFonts.UiSans, RuntimeJsonReaders.ResolveFontFamilyId("UiSans"));
        Assert.Equal(BuiltinFonts.Mono, RuntimeJsonReaders.ResolveFontFamilyId("Mono"));
        Assert.Equal("fonttest.jost", RuntimeJsonReaders.ResolveFontFamilyId("fonttest.jost"));
    }

    [Fact]
    public async Task SceneRuntime_EngineDeserializers_apply_on_spawn_when_renderer_present()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_engine_scene_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var json = """
                   {"schemaVersion":1,"entities":[
                     {"logicalId":"dddddddd-dddd-dddd-dddd-dddddddddddd","components":[
                       {"type":"cyberland.engine/transform","data":{"localX":1,"localY":2}},
                       {"type":"cyberland.engine/camera2d","data":{"viewportWidth":640,"viewportHeight":480}},
                       {"type":"cyberland.engine/sprite","data":{"halfExtents":{"x":4,"y":6},"layer":50,"space":"ViewportSpace","visible":false,"colorMultiply":{"x":1,"y":0.5,"z":0.25,"w":1},"emissiveTint":{"x":0.1,"y":0.2,"z":0.3},"emissiveIntensity":1.2}},
                       {"type":"cyberland.engine/viewport-anchor-2d","data":{"anchor":"TopLeft","syncSpriteHalfExtentsToViewport":true}},
                       {"type":"cyberland.engine/bitmap-text","data":{"content":"hi","sizePixels":14,"bold":true}},
                       {"type":"cyberland.engine/ambient-light","data":{"intensity":0.2}},
                       {"type":"cyberland.engine/directional-light","data":{}},
                       {"type":"cyberland.engine/spot-light","data":{}},
                       {"type":"cyberland.engine/point-light","data":{}},
                       {"type":"cyberland.engine/post-process-volume","data":{"hasBloomGain":true,"bloomGain":2}},
                       {"type":"cyberland.engine/global-post-process","data":{"bloomEnabled":false}},
                       {"type":"cyberland.engine/audio-emitter","data":{"clip":"Sounds/a.wav","space":"direct","bus":"sfx","loop":true,"playOnEnable":true,"gain":0.5,"pitch":1.1,"priority":2,"fadeInSeconds":0.2,"refDistance":10,"maxDistance":100,"rolloff":1.5,"pauseWithGameplay":false}},
                       {"type":"cyberland.engine/music","data":{"clip":"Sounds/m.wav","bus":"music","loop":true,"crossfadeSeconds":0.5,"pauseWithGameplay":false,"priority":1}},
                       {"type":"cyberland.engine/global-audio-environment","data":{"priority":1,"blendSeconds":0.5,"lowPassHz":1000,"busGains":[{"bus":"music","gain":1.1}]}},
                       {"type":"cyberland.engine/audio-environment-volume","data":{"halfExtents":{"x":40,"y":50},"priority":2,"overrides":{"lowPassHz":500,"busGains":[{"bus":"sfx","gain":0.8}]}}},
                       {"type":"cyberland.engine/audio-listener-override","data":{"active":true,"priority":3}}
                     ]}
                   ]}
                   """;
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "engine.json"), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);

        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var rootWorld = new World();
        var rootSched = new SystemScheduler(new ParallelismSettings());
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, rootWorld, rootSched);
        var rt = host.RuntimeScenes!;

        var result = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/engine.json");
        Assert.True(result.Succeeded);

        var eid = LogicalActorLookup.Resolve(rootWorld, "dddddddd-dddd-dddd-dddd-dddddddddddd");
        Assert.True(rootWorld.Has<Camera2D>(eid));
        Assert.True(rootWorld.Has<Sprite>(eid));
        Assert.True(rootWorld.Has<BitmapText>(eid));
        Assert.True(rootWorld.Has<AmbientLightSource>(eid));
        Assert.True(rootWorld.Has<GlobalPostProcessSource>(eid));
        Assert.True(rootWorld.Get<GlobalPostProcessSource>(eid).Settings.Shadows.Enabled);
        Assert.False(rootWorld.Get<Sprite>(eid).Visible);
        Assert.Equal(50, rootWorld.Get<Sprite>(eid).Layer);
        Assert.True(rootWorld.Has<AudioEmitterSource>(eid));
        Assert.Equal(AudioSpace.Direct, rootWorld.Get<AudioEmitterSource>(eid).Space);
        Assert.True(rootWorld.Has<MusicSource>(eid));
        Assert.True(rootWorld.Has<GlobalAudioEnvironmentSource>(eid));
        Assert.True(rootWorld.Has<AudioEnvironmentVolumeSource>(eid));
        Assert.True(rootWorld.Has<AudioListenerOverride>(eid));
        Assert.True(rootWorld.Get<AudioListenerOverride>(eid).Active);

        // Idempotent: second spawn still uses registered engine deserializers.
        var again = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/engine.json");
        Assert.True(again.Succeeded);

        var loadId = rt.BeginLoad(new SceneLoadDescriptor { ScenePath = "Content/Scenes/engine.json" });
        var pump = await rt.PumpAsync(loadId, new SceneLoadPumpOptions { MaxElapsed = TimeSpan.FromSeconds(5) });
        Assert.False(pump.Failed);

        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "half.json"),
            """{"schemaVersion":1,"entities":[{"components":[{"type":"cyberland.engine/transform","data":{}},{"type":"cyberland.engine/sprite","data":{"halfExtent":12}}]}]}""");
        var half = await rt.SpawnIntoWorldAsync(rootWorld, "Content/Scenes/half.json");
        Assert.True(half.Succeeded);
    }

    [Fact]
    public async Task SceneRuntime_EngineDeserializers_resolve_camera_follow_trigger_and_ui_root()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_engine_scene3_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        var json = """
                   {"schemaVersion":1,"entities":[
                     {"logicalId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01","components":[
                       {"type":"cyberland.engine/camera2d","data":{"viewportWidth":800,"viewportHeight":600,"priority":7,"matchPresentationViewport":true}},
                       {"type":"cyberland.engine/camera-follow-2d","data":{"targetLogicalId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02","enabled":false,"followLerp":0.5,"clampToBounds":true,"boundsMinWorld":{"x":1,"y":2},"boundsMaxWorld":{"x":3,"y":4}}}
                     ]},
                     {"logicalId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04","components":[
                       {"type":"cyberland.engine/camera-follow-2d","data":{"targetLogicalId":"   "}}
                     ]},
                     {"logicalId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02","components":[
                       {"type":"cyberland.engine/trigger","data":{"shape":"Rectangle","halfExtents":{"x":8,"y":9}}},
                       {"type":"cyberland.engine/sprite-localized-asset","data":{"canonicalAlbedoPath":"Textures/x.png","keepExistingOnMissing":false}},
                       {"type":"cyberland.engine/sprite-atlas-binding","data":{"manifestPath":"Textures/Atlases/run.atlas.json","sheet":"run","localeInvariant":true,"reloadGeneration":2}}
                     ]},
                     {"logicalId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03","components":[
                       {"type":"cyberland.engine/ui-document-root","data":{"sortKeyBase":901,"visible":false}}
                     ]}
                   ]}
                   """;
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Scenes", "follow.json"), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);

        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));
        var result = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/follow.json");
        Assert.True(result.Succeeded);

        var camera = LogicalActorLookup.Resolve(world, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01");
        var target = LogicalActorLookup.Resolve(world, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02");
        var cam = world.Get<Camera2D>(camera);
        Assert.Equal(7, cam.Priority);
        Assert.Equal(new Vector2D<int>(800, 600), cam.PresentationViewportSizeWorld);

        var follow = world.Get<CameraFollow2D>(camera);
        Assert.Equal(target, follow.Target);
        Assert.False(follow.Enabled);
        Assert.Equal(0.5f, follow.FollowLerp);
        Assert.True(follow.ClampToBounds);
        Assert.Equal(1f, follow.BoundsMinWorld.X);

        var noopFollow = LogicalActorLookup.Resolve(world, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04");
        Assert.Equal(default, world.Get<CameraFollow2D>(noopFollow).Target);

        var trigger = world.Get<Trigger>(target);
        Assert.Equal(TriggerShapeKind.Rectangle, trigger.Shape);
        Assert.Equal(8f, trigger.HalfExtents.X);

        var asset = world.Get<SpriteLocalizedAsset>(target);
        Assert.Equal("Textures/x.png", asset.CanonicalAlbedoPath);
        Assert.False(asset.KeepExistingOnMissing);

        var binding = world.Get<SpriteAtlasBinding>(target);
        Assert.Equal("Textures/Atlases/run.atlas.json", binding.CanonicalManifestPath);
        Assert.Equal("run", binding.SheetName);
        Assert.True(binding.LocaleInvariant);
        Assert.Equal(2, binding.ReloadGeneration);

        var uiRoot = LogicalActorLookup.Resolve(world, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03");
        var docRoot = world.Get<UiDocumentRoot>(uiRoot);
        Assert.Equal(901f, docRoot.SortKeyBase);
        Assert.False(docRoot.Visible);
    }

    [Fact]
    public async Task SceneRuntime_camera_follow_unknown_target_fails_spawn()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_engine_scene4_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "bad.json"),
            """
            {"schemaVersion":1,"entities":[{"logicalId":"ffffffff-ffff-ffff-ffff-ffffffffff01","components":[
              {"type":"cyberland.engine/camera-follow-2d","data":{"targetLogicalId":"ffffffff-ffff-ffff-ffff-ffffffffff99"}}
            ]}]}
            """);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));
        var result = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/bad.json");
        Assert.False(result.Succeeded);
        Assert.Contains("targetLogicalId", result.ErrorMessage ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void EngineSceneComponentDeserializers_Register_validates_arguments()
    {
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        host.InitializeRuntimeScenes(new VirtualFileSystem(), new ParallelismSettings(), () => null, new World(), new SystemScheduler(new ParallelismSettings()));
        var rt = host.RuntimeScenes!;
        Assert.Throws<ArgumentNullException>(() => EngineSceneComponentDeserializers.Register(null!, host.Renderer));
        Assert.Throws<ArgumentNullException>(() => EngineSceneComponentDeserializers.Register(rt, null!));
    }

    [Fact]
    public async Task SceneRuntime_EnsureEngineDeserializers_skips_when_renderer_absent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_engine_scene2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "t.json"),
            """{"schemaVersion":1,"entities":[{"components":[{"type":"cyberland.engine/transform","data":{}}]}]}""");
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var host = new GameHostServices();
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));
        var result = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/t.json");
        Assert.True(result.Succeeded);
    }
}
