using System.Text.Json;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Serialization;
using Cyberland.Engine.Scene;
using Cyberland.Engine.UI.Ecs;
using Silk.NET.Maths;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>Registers built-in <c>cyberland.engine/*</c> scene JSON component deserializers (requires <see cref="IRenderer"/> for sprite defaults).</summary>
public static class EngineSceneComponentDeserializers
{
    /// <summary>Registers all stock engine component types on <paramref name="scenes"/>.</summary>
    public static void Register(ISceneRuntime scenes, IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(renderer);
        var white = renderer.WhiteTextureId;
        var defaultNormal = renderer.DefaultNormalTextureId;

        scenes.RegisterComponentDeserializer("cyberland.engine/camera2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            var w = RuntimeJsonReaders.ReadInt(ctx.Data, "viewportWidth", 1280);
            var h = RuntimeJsonReaders.ReadInt(ctx.Data, "viewportHeight", 720);
            var cam = Camera2D.Create(new Vector2D<int>(w, h));
            cam.Priority = RuntimeJsonReaders.ReadInt(ctx.Data, "priority", cam.Priority);
            if (RuntimeJsonReaders.ReadBool(ctx.Data, "matchPresentationViewport", false))
                cam.PresentationViewportSizeWorld = new Vector2D<int>(w, h);
            ctx.World.GetOrAdd<Camera2D>(ctx.EntityId) = cam;
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/sprite", (in SceneComponentDeserializeContext ctx) =>
        {
            ref var sprite = ref ctx.World.GetOrAdd<Sprite>(ctx.EntityId);
            var half = RuntimeJsonReaders.ReadFloat(ctx.Data, "halfExtent", 16f);
            sprite = Sprite.DefaultWhiteUnlit(white, defaultNormal, new Vector2D<float>(half, half));
            sprite.Layer = RuntimeJsonReaders.ReadInt(ctx.Data, "layer", (int)SpriteLayer.World);
            sprite.SortKey = RuntimeJsonReaders.ReadFloat(ctx.Data, "sortKey", 0f);
            sprite.Visible = RuntimeJsonReaders.ReadBool(ctx.Data, "visible", true);
            sprite.Space = RuntimeJsonReaders.ReadEnum(ctx.Data, "space", CoordinateSpace.WorldSpace);
            if (RuntimeJsonReaders.TryReadVec2(ctx.Data, "halfExtents", out var he))
                sprite.HalfExtents = he;
            if (RuntimeJsonReaders.TryReadVec4(ctx.Data, "colorMultiply", out var cm))
                sprite.ColorMultiply = cm;
            if (RuntimeJsonReaders.TryReadVec3(ctx.Data, "emissiveTint", out var et))
                sprite.EmissiveTint = et;
            sprite.EmissiveIntensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "emissiveIntensity", sprite.EmissiveIntensity);
            sprite.Alpha = RuntimeJsonReaders.ReadFloat(ctx.Data, "alpha", sprite.Alpha);
            sprite.Transparent = RuntimeJsonReaders.ReadBool(ctx.Data, "transparent", sprite.Transparent);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/viewport-anchor-2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var anchor = ref ctx.World.GetOrAdd<ViewportAnchor2D>(ctx.EntityId);
            anchor.Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true);
            anchor.ContentSpace = RuntimeJsonReaders.ReadEnum(ctx.Data, "contentSpace", CoordinateSpace.WorldSpace);
            anchor.Anchor = RuntimeJsonReaders.ReadEnum(ctx.Data, "anchor", ViewportAnchorPreset.Center);
            anchor.OffsetX = RuntimeJsonReaders.ReadFloat(ctx.Data, "offsetX", 0f);
            anchor.OffsetY = RuntimeJsonReaders.ReadFloat(ctx.Data, "offsetY", 0f);
            anchor.SyncSpriteHalfExtentsToViewport = RuntimeJsonReaders.ReadBool(ctx.Data, "syncSpriteHalfExtentsToViewport", false);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/bitmap-text", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var text = ref ctx.World.GetOrAdd<BitmapText>(ctx.EntityId);
            text.Visible = RuntimeJsonReaders.ReadBool(ctx.Data, "visible", true);
            text.IsLocalizationKey = RuntimeJsonReaders.ReadBool(ctx.Data, "isLocalizationKey", false);
            text.Content = RuntimeJsonReaders.ReadString(ctx.Data, "content") ?? "";
            text.SortKey = RuntimeJsonReaders.ReadFloat(ctx.Data, "sortKey", 0f);
            text.CoordinateSpace = RuntimeJsonReaders.ReadEnum(ctx.Data, "coordinateSpace", BitmapText.HudDefaultCoordinateSpace);
            var family = RuntimeJsonReaders.ResolveFontFamilyId(RuntimeJsonReaders.ReadString(ctx.Data, "fontFamily"));
            var size = RuntimeJsonReaders.ReadFloat(ctx.Data, "sizePixels", 16f);
            var color = RuntimeJsonReaders.TryReadVec4(ctx.Data, "color", out var c)
                ? c
                : new Vector4D<float>(1f, 1f, 1f, 1f);
            text.Style = new TextStyle(
                family,
                size,
                color,
                RuntimeJsonReaders.ReadBool(ctx.Data, "bold", false),
                RuntimeJsonReaders.ReadBool(ctx.Data, "italic", false));
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/ambient-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<AmbientLightSource>(ctx.EntityId) = new AmbientLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Color = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/directional-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<DirectionalLightSource>(ctx.EntityId) = new DirectionalLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Color = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/spot-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SpotLightSource>(ctx.EntityId) = new SpotLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Radius = RuntimeJsonReaders.ReadFloat(ctx.Data, "radius", 100f),
                InnerConeRadians = RuntimeJsonReaders.ReadFloat(ctx.Data, "innerConeRadians", MathF.PI / 4f),
                OuterConeRadians = RuntimeJsonReaders.ReadFloat(ctx.Data, "outerConeRadians", MathF.PI / 2f),
                Color = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/point-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<PointLightSource>(ctx.EntityId) = new PointLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Radius = RuntimeJsonReaders.ReadFloat(ctx.Data, "radius", 100f),
                Color = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                FalloffExponent = RuntimeJsonReaders.ReadFloat(ctx.Data, "falloffExponent", 2f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/post-process-volume", static (in SceneComponentDeserializeContext ctx) =>
        {
            var halfW = RuntimeJsonReaders.ReadFloat(ctx.Data, "halfExtentsX", 640f);
            var halfH = RuntimeJsonReaders.ReadFloat(ctx.Data, "halfExtentsY", 360f);
            ctx.World.GetOrAdd<PostProcessVolumeSource>(ctx.EntityId) = new PostProcessVolumeSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Volume = new PostProcessVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(halfW, halfH),
                    Priority = RuntimeJsonReaders.ReadInt(ctx.Data, "priority", 0),
                    Overrides = new PostProcessOverrides
                    {
                        HasBloomGain = RuntimeJsonReaders.ReadBool(ctx.Data, "hasBloomGain", false),
                        BloomGain = RuntimeJsonReaders.ReadFloat(ctx.Data, "bloomGain", 1f),
                        HasExposure = false,
                        HasSaturation = false
                    }
                }
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/global-post-process", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<GlobalPostProcessSource>(ctx.EntityId) = new GlobalPostProcessSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Priority = RuntimeJsonReaders.ReadInt(ctx.Data, "priority", 0),
                Settings = new GlobalPostProcessSettings
                {
                    BloomEnabled = RuntimeJsonReaders.ReadBool(ctx.Data, "bloomEnabled", true),
                    BloomRadius = RuntimeJsonReaders.ReadFloat(ctx.Data, "bloomRadius", 1f),
                    BloomGain = RuntimeJsonReaders.ReadFloat(ctx.Data, "bloomGain", 1f),
                    BloomExtractThreshold = RuntimeJsonReaders.ReadFloat(ctx.Data, "bloomExtractThreshold", 0.5f),
                    BloomExtractKnee = RuntimeJsonReaders.ReadFloat(ctx.Data, "bloomExtractKnee", 0.5f),
                    EmissiveToHdrGain = RuntimeJsonReaders.ReadFloat(ctx.Data, "emissiveToHdrGain", 0.5f),
                    EmissiveToBloomGain = RuntimeJsonReaders.ReadFloat(ctx.Data, "emissiveToBloomGain", 0.5f),
                    Exposure = RuntimeJsonReaders.ReadFloat(ctx.Data, "exposure", 1f),
                    Saturation = RuntimeJsonReaders.ReadFloat(ctx.Data, "saturation", 1f),
                    TonemapEnabled = RuntimeJsonReaders.ReadBool(ctx.Data, "tonemapEnabled", true),
                    ColorGradingShadows = RuntimeJsonReaders.TryReadVec3(ctx.Data, "colorGradingShadows", out var sh) ? sh : Vector3D<float>.One,
                    ColorGradingMidtones = RuntimeJsonReaders.TryReadVec3(ctx.Data, "colorGradingMidtones", out var mid) ? mid : Vector3D<float>.One,
                    ColorGradingHighlights = RuntimeJsonReaders.TryReadVec3(ctx.Data, "colorGradingHighlights", out var hi) ? hi : Vector3D<float>.One
                }
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/trigger", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<Trigger>(ctx.EntityId) = new Trigger
            {
                Enabled = RuntimeJsonReaders.ReadBool(ctx.Data, "enabled", true),
                Shape = RuntimeJsonReaders.ReadEnum(ctx.Data, "shape", TriggerShapeKind.Circle),
                Radius = RuntimeJsonReaders.ReadFloat(ctx.Data, "radius", 16f),
                HalfExtents = RuntimeJsonReaders.TryReadVec2(ctx.Data, "halfExtents", out var he)
                    ? he
                    : new Vector2D<float>(16f, 16f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/camera-follow-2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var follow = ref ctx.World.GetOrAdd<CameraFollow2D>(ctx.EntityId);
            follow.Enabled = RuntimeJsonReaders.ReadBool(ctx.Data, "enabled", true);
            follow.OffsetWorld = RuntimeJsonReaders.TryReadVec2(ctx.Data, "offsetWorld", out var off)
                ? off
                : default;
            follow.FollowLerp = RuntimeJsonReaders.ReadFloat(ctx.Data, "followLerp", 0.2f);
            follow.ClampToBounds = RuntimeJsonReaders.ReadBool(ctx.Data, "clampToBounds", false);
            if (RuntimeJsonReaders.TryReadVec2(ctx.Data, "boundsMinWorld", out var bmin))
                follow.BoundsMinWorld = bmin;
            if (RuntimeJsonReaders.TryReadVec2(ctx.Data, "boundsMaxWorld", out var bmax))
                follow.BoundsMaxWorld = bmax;
            if (ctx.SpawnSession is not null
                && ctx.Data.TryGetProperty("targetLogicalId", out var jt)
                && jt.ValueKind == JsonValueKind.String)
            {
                var targetId = jt.GetString();
                if (!string.IsNullOrWhiteSpace(targetId))
                    ctx.SpawnSession.PendingCameraFollowTargets.Add((ctx.EntityId, targetId));
            }
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/sprite-localized-asset", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SpriteLocalizedAsset>(ctx.EntityId) = new SpriteLocalizedAsset
            {
                CanonicalAlbedoPath = RuntimeJsonReaders.ReadString(ctx.Data, "canonicalAlbedoPath") ?? "",
                ReloadGeneration = RuntimeJsonReaders.ReadInt(ctx.Data, "reloadGeneration", 1),
                LoadedGeneration = RuntimeJsonReaders.ReadInt(ctx.Data, "loadedGeneration", 0),
                KeepExistingOnMissing = RuntimeJsonReaders.ReadBool(ctx.Data, "keepExistingOnMissing", true)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/ui-document-root", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<UiDocumentRoot>(ctx.EntityId) = new UiDocumentRoot
            {
                Visible = RuntimeJsonReaders.ReadBool(ctx.Data, "visible", true),
                CoordinateSpace = RuntimeJsonReaders.ReadEnum(ctx.Data, "coordinateSpace", CoordinateSpace.PresentationViewportSpace),
                RootPreset = RuntimeJsonReaders.ReadEnum(ctx.Data, "rootPreset", UiDocumentRootPreset.FullViewport),
                SortKeyBase = RuntimeJsonReaders.ReadFloat(ctx.Data, "sortKeyBase", 800f)
            };

            var uiPath = RuntimeJsonReaders.ReadString(ctx.Data, "uiPath");
            if (ctx.SpawnSession is not null && !string.IsNullOrWhiteSpace(uiPath))
                ctx.SpawnSession.PendingUiDocuments.Add((ctx.EntityId, uiPath));
        });
    }
}
