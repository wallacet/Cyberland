using System.Text.Json;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes.Serialization;
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
            var w = SceneComponentJson.ReadInt(ctx.Data, "viewportWidth", 1280);
            var h = SceneComponentJson.ReadInt(ctx.Data, "viewportHeight", 720);
            var cam = Camera2D.Create(new Vector2D<int>(w, h));
            cam.Priority = SceneComponentJson.ReadInt(ctx.Data, "priority", cam.Priority);
            if (SceneComponentJson.ReadBool(ctx.Data, "matchPresentationViewport", false))
                cam.PresentationViewportSizeWorld = new Vector2D<int>(w, h);
            ctx.World.GetOrAdd<Camera2D>(ctx.EntityId) = cam;
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/sprite", (in SceneComponentDeserializeContext ctx) =>
        {
            ref var sprite = ref ctx.World.GetOrAdd<Sprite>(ctx.EntityId);
            var half = SceneComponentJson.ReadFloat(ctx.Data, "halfExtent", 16f);
            sprite = Sprite.DefaultWhiteUnlit(white, defaultNormal, new Vector2D<float>(half, half));
            sprite.Layer = SceneComponentJson.ReadInt(ctx.Data, "layer", (int)SpriteLayer.World);
            sprite.SortKey = SceneComponentJson.ReadFloat(ctx.Data, "sortKey", 0f);
            sprite.Visible = SceneComponentJson.ReadBool(ctx.Data, "visible", true);
            sprite.Space = SceneComponentJson.ReadEnum(ctx.Data, "space", CoordinateSpace.WorldSpace);
            if (SceneComponentJson.TryReadVec2(ctx.Data, "halfExtents", out var he))
                sprite.HalfExtents = he;
            if (SceneComponentJson.TryReadVec4(ctx.Data, "colorMultiply", out var cm))
                sprite.ColorMultiply = cm;
            if (SceneComponentJson.TryReadVec3(ctx.Data, "emissiveTint", out var et))
                sprite.EmissiveTint = et;
            sprite.EmissiveIntensity = SceneComponentJson.ReadFloat(ctx.Data, "emissiveIntensity", sprite.EmissiveIntensity);
            sprite.Alpha = SceneComponentJson.ReadFloat(ctx.Data, "alpha", sprite.Alpha);
            sprite.Transparent = SceneComponentJson.ReadBool(ctx.Data, "transparent", sprite.Transparent);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/viewport-anchor-2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var anchor = ref ctx.World.GetOrAdd<ViewportAnchor2D>(ctx.EntityId);
            anchor.Active = SceneComponentJson.ReadBool(ctx.Data, "active", true);
            anchor.ContentSpace = SceneComponentJson.ReadEnum(ctx.Data, "contentSpace", CoordinateSpace.WorldSpace);
            anchor.Anchor = SceneComponentJson.ReadEnum(ctx.Data, "anchor", ViewportAnchorPreset.Center);
            anchor.OffsetX = SceneComponentJson.ReadFloat(ctx.Data, "offsetX", 0f);
            anchor.OffsetY = SceneComponentJson.ReadFloat(ctx.Data, "offsetY", 0f);
            anchor.SyncSpriteHalfExtentsToViewport = SceneComponentJson.ReadBool(ctx.Data, "syncSpriteHalfExtentsToViewport", false);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/bitmap-text", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var text = ref ctx.World.GetOrAdd<BitmapText>(ctx.EntityId);
            text.Visible = SceneComponentJson.ReadBool(ctx.Data, "visible", true);
            text.IsLocalizationKey = SceneComponentJson.ReadBool(ctx.Data, "isLocalizationKey", false);
            text.Content = SceneComponentJson.ReadString(ctx.Data, "content") ?? "";
            text.SortKey = SceneComponentJson.ReadFloat(ctx.Data, "sortKey", 0f);
            text.CoordinateSpace = SceneComponentJson.ReadEnum(ctx.Data, "coordinateSpace", BitmapText.HudDefaultCoordinateSpace);
            var family = SceneComponentJson.ReadString(ctx.Data, "fontFamily") ?? BuiltinFonts.UiSans;
            var size = SceneComponentJson.ReadFloat(ctx.Data, "sizePixels", 16f);
            var color = SceneComponentJson.TryReadVec4(ctx.Data, "color", out var c)
                ? c
                : new Vector4D<float>(1f, 1f, 1f, 1f);
            text.Style = new TextStyle(
                family,
                size,
                color,
                SceneComponentJson.ReadBool(ctx.Data, "bold", false),
                SceneComponentJson.ReadBool(ctx.Data, "italic", false));
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/ambient-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<AmbientLightSource>(ctx.EntityId) = new AmbientLightSource
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Color = SceneComponentJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneComponentJson.ReadFloat(ctx.Data, "intensity", 1f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/directional-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<DirectionalLightSource>(ctx.EntityId) = new DirectionalLightSource
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Color = SceneComponentJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneComponentJson.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = SceneComponentJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/spot-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SpotLightSource>(ctx.EntityId) = new SpotLightSource
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Radius = SceneComponentJson.ReadFloat(ctx.Data, "radius", 100f),
                InnerConeRadians = SceneComponentJson.ReadFloat(ctx.Data, "innerConeRadians", MathF.PI / 4f),
                OuterConeRadians = SceneComponentJson.ReadFloat(ctx.Data, "outerConeRadians", MathF.PI / 2f),
                Color = SceneComponentJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneComponentJson.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = SceneComponentJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/point-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<PointLightSource>(ctx.EntityId) = new PointLightSource
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Radius = SceneComponentJson.ReadFloat(ctx.Data, "radius", 100f),
                Color = SceneComponentJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneComponentJson.ReadFloat(ctx.Data, "intensity", 1f),
                FalloffExponent = SceneComponentJson.ReadFloat(ctx.Data, "falloffExponent", 2f),
                CastsShadow = SceneComponentJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/post-process-volume", static (in SceneComponentDeserializeContext ctx) =>
        {
            var halfW = SceneComponentJson.ReadFloat(ctx.Data, "halfExtentsX", 640f);
            var halfH = SceneComponentJson.ReadFloat(ctx.Data, "halfExtentsY", 360f);
            ctx.World.GetOrAdd<PostProcessVolumeSource>(ctx.EntityId) = new PostProcessVolumeSource
            {
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Volume = new PostProcessVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(halfW, halfH),
                    Priority = SceneComponentJson.ReadInt(ctx.Data, "priority", 0),
                    Overrides = new PostProcessOverrides
                    {
                        HasBloomGain = SceneComponentJson.ReadBool(ctx.Data, "hasBloomGain", false),
                        BloomGain = SceneComponentJson.ReadFloat(ctx.Data, "bloomGain", 1f),
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
                Active = SceneComponentJson.ReadBool(ctx.Data, "active", true),
                Priority = SceneComponentJson.ReadInt(ctx.Data, "priority", 0),
                Settings = new GlobalPostProcessSettings
                {
                    BloomEnabled = SceneComponentJson.ReadBool(ctx.Data, "bloomEnabled", true),
                    BloomRadius = SceneComponentJson.ReadFloat(ctx.Data, "bloomRadius", 1f),
                    BloomGain = SceneComponentJson.ReadFloat(ctx.Data, "bloomGain", 1f),
                    BloomExtractThreshold = SceneComponentJson.ReadFloat(ctx.Data, "bloomExtractThreshold", 0.5f),
                    BloomExtractKnee = SceneComponentJson.ReadFloat(ctx.Data, "bloomExtractKnee", 0.5f),
                    EmissiveToHdrGain = SceneComponentJson.ReadFloat(ctx.Data, "emissiveToHdrGain", 0.5f),
                    EmissiveToBloomGain = SceneComponentJson.ReadFloat(ctx.Data, "emissiveToBloomGain", 0.5f),
                    Exposure = SceneComponentJson.ReadFloat(ctx.Data, "exposure", 1f),
                    Saturation = SceneComponentJson.ReadFloat(ctx.Data, "saturation", 1f),
                    TonemapEnabled = SceneComponentJson.ReadBool(ctx.Data, "tonemapEnabled", true),
                    ColorGradingShadows = SceneComponentJson.TryReadVec3(ctx.Data, "colorGradingShadows", out var sh) ? sh : Vector3D<float>.One,
                    ColorGradingMidtones = SceneComponentJson.TryReadVec3(ctx.Data, "colorGradingMidtones", out var mid) ? mid : Vector3D<float>.One,
                    ColorGradingHighlights = SceneComponentJson.TryReadVec3(ctx.Data, "colorGradingHighlights", out var hi) ? hi : Vector3D<float>.One
                }
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/trigger", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<Trigger>(ctx.EntityId) = new Trigger
            {
                Enabled = SceneComponentJson.ReadBool(ctx.Data, "enabled", true),
                Shape = SceneComponentJson.ReadEnum(ctx.Data, "shape", TriggerShapeKind.Circle),
                Radius = SceneComponentJson.ReadFloat(ctx.Data, "radius", 16f),
                HalfExtents = SceneComponentJson.TryReadVec2(ctx.Data, "halfExtents", out var he)
                    ? he
                    : new Vector2D<float>(16f, 16f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/camera-follow-2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var follow = ref ctx.World.GetOrAdd<CameraFollow2D>(ctx.EntityId);
            follow.Enabled = SceneComponentJson.ReadBool(ctx.Data, "enabled", true);
            follow.OffsetWorld = SceneComponentJson.TryReadVec2(ctx.Data, "offsetWorld", out var off)
                ? off
                : default;
            follow.FollowLerp = SceneComponentJson.ReadFloat(ctx.Data, "followLerp", 0.2f);
            follow.ClampToBounds = SceneComponentJson.ReadBool(ctx.Data, "clampToBounds", false);
            if (SceneComponentJson.TryReadVec2(ctx.Data, "boundsMinWorld", out var bmin))
                follow.BoundsMinWorld = bmin;
            if (SceneComponentJson.TryReadVec2(ctx.Data, "boundsMaxWorld", out var bmax))
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
                CanonicalAlbedoPath = SceneComponentJson.ReadString(ctx.Data, "canonicalAlbedoPath") ?? "",
                ReloadGeneration = SceneComponentJson.ReadInt(ctx.Data, "reloadGeneration", 1),
                LoadedGeneration = SceneComponentJson.ReadInt(ctx.Data, "loadedGeneration", 0),
                KeepExistingOnMissing = SceneComponentJson.ReadBool(ctx.Data, "keepExistingOnMissing", true)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/ui-document-root", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<UiDocumentRoot>(ctx.EntityId) = new UiDocumentRoot
            {
                Visible = SceneComponentJson.ReadBool(ctx.Data, "visible", true),
                CoordinateSpace = SceneComponentJson.ReadEnum(ctx.Data, "coordinateSpace", CoordinateSpace.PresentationViewportSpace),
                RootPreset = SceneComponentJson.ReadEnum(ctx.Data, "rootPreset", UiDocumentRootPreset.FullViewport),
                SortKeyBase = SceneComponentJson.ReadFloat(ctx.Data, "sortKeyBase", 800f)
            };
        });
    }
}
