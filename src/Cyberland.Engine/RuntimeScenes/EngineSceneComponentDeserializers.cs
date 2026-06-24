using System.Text.Json;
using Cyberland.Engine.Diagnostics;
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
            sprite.CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", sprite.CastsShadow);
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
            var rawColor = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One;
            ctx.World.GetOrAdd<AmbientLightSource>(ctx.EntityId) = new AmbientLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Color = ClampLightColor(rawColor, "ambient-light"),
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/directional-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            var rawColor = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One;
            ctx.World.GetOrAdd<DirectionalLightSource>(ctx.EntityId) = new DirectionalLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Color = ClampLightColor(rawColor, "directional-light"),
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", true)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/spot-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            var rawColor = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One;
            ctx.World.GetOrAdd<SpotLightSource>(ctx.EntityId) = new SpotLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Radius = RuntimeJsonReaders.ReadFloat(ctx.Data, "radius", 100f),
                InnerConeRadians = RuntimeJsonReaders.ReadFloat(ctx.Data, "innerConeRadians", MathF.PI / 4f),
                OuterConeRadians = RuntimeJsonReaders.ReadFloat(ctx.Data, "outerConeRadians", MathF.PI / 2f),
                Color = ClampLightColor(rawColor, "spot-light"),
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                FalloffExponent = RuntimeJsonReaders.ReadFloat(ctx.Data, "falloffExponent", 2f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", true)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/point-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            var rawColor = RuntimeJsonReaders.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One;
            ctx.World.GetOrAdd<PointLightSource>(ctx.EntityId) = new PointLightSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Radius = RuntimeJsonReaders.ReadFloat(ctx.Data, "radius", 100f),
                Color = ClampLightColor(rawColor, "point-light"),
                Intensity = RuntimeJsonReaders.ReadFloat(ctx.Data, "intensity", 1f),
                FalloffExponent = RuntimeJsonReaders.ReadFloat(ctx.Data, "falloffExponent", 2f),
                CastsShadow = RuntimeJsonReaders.ReadBool(ctx.Data, "castsShadow", true)
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
                    Overrides = ReadPostProcessOverrides(ctx.Data)
                }
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/global-post-process", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<GlobalPostProcessSource>(ctx.EntityId) = new GlobalPostProcessSource
            {
                Active = RuntimeJsonReaders.ReadBool(ctx.Data, "active", true),
                Priority = RuntimeJsonReaders.ReadInt(ctx.Data, "priority", 0),
                Settings = ReadGlobalPostProcessSettings(ctx.Data)
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

        scenes.RegisterComponentDeserializer("cyberland.engine/sprite-atlas-binding", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SpriteAtlasBinding>(ctx.EntityId) = new SpriteAtlasBinding
            {
                CanonicalManifestPath = RuntimeJsonReaders.ReadString(ctx.Data, "manifestPath") ?? "",
                RegionName = RuntimeJsonReaders.ReadString(ctx.Data, "region") ?? "",
                AnimationName = RuntimeJsonReaders.ReadString(ctx.Data, "animation") ?? "",
                SheetName = RuntimeJsonReaders.ReadString(ctx.Data, "sheet") ?? "",
                ReloadGeneration = RuntimeJsonReaders.ReadInt(ctx.Data, "reloadGeneration", 1),
                LoadedGeneration = RuntimeJsonReaders.ReadInt(ctx.Data, "loadedGeneration", 0),
                LocaleInvariant = RuntimeJsonReaders.ReadBool(ctx.Data, "localeInvariant", false)
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

    /// <summary>Reads sparse override fields from JSON; omitted fields leave their <c>Has*</c> flag false (no override).</summary>
    internal static PostProcessOverrides ReadPostProcessOverrides(JsonElement data)
    {
        var o = new PostProcessOverrides();

        o.HasBloomGain = RuntimeJsonReaders.ReadBool(data, "hasBloomGain", false);
        o.BloomGain = RuntimeJsonReaders.ReadFloat(data, "bloomGain", 1f);
        o.HasBloomRadius = RuntimeJsonReaders.ReadBool(data, "hasBloomRadius", false);
        o.BloomRadius = RuntimeJsonReaders.ReadFloat(data, "bloomRadius", 1f);
        o.HasEmissiveToHdrGain = RuntimeJsonReaders.ReadBool(data, "hasEmissiveToHdrGain", false);
        o.EmissiveToHdrGain = RuntimeJsonReaders.ReadFloat(data, "emissiveToHdrGain", 1f);
        o.HasBloomSourceGain = RuntimeJsonReaders.ReadBool(data, "hasBloomSourceGain", false);
        o.BloomSourceGain = RuntimeJsonReaders.ReadFloat(data, "bloomSourceGain", 1f);
        o.HasExposure = RuntimeJsonReaders.ReadBool(data, "hasExposure", false);
        o.Exposure = RuntimeJsonReaders.ReadFloat(data, "exposure", 1f);
        o.HasSaturation = RuntimeJsonReaders.ReadBool(data, "hasSaturation", false);
        o.Saturation = RuntimeJsonReaders.ReadFloat(data, "saturation", 1f);
        o.HasShadows = RuntimeJsonReaders.ReadBool(data, "hasShadows", false);
        if (o.HasShadows)
            o.Shadows = ReadShadowSettings(data, ShadowSettings.Default);
        o.HasTonemapEnabled = RuntimeJsonReaders.ReadBool(data, "hasTonemapEnabled", false);
        o.TonemapEnabled = RuntimeJsonReaders.ReadBool(data, "tonemapEnabled", true);
        o.HasColorGradingShadows = RuntimeJsonReaders.TryReadVec3(data, "colorGradingShadowsOverride", out var cgs);
        if (o.HasColorGradingShadows) o.ColorGradingShadows = cgs;
        o.HasColorGradingMidtones = RuntimeJsonReaders.TryReadVec3(data, "colorGradingMidtonesOverride", out var cgm);
        if (o.HasColorGradingMidtones) o.ColorGradingMidtones = cgm;
        o.HasColorGradingHighlights = RuntimeJsonReaders.TryReadVec3(data, "colorGradingHighlightsOverride", out var cgh);
        if (o.HasColorGradingHighlights) o.ColorGradingHighlights = cgh;
        o.HasBloomEnabled = RuntimeJsonReaders.ReadBool(data, "hasBloomEnabled", false);
        o.BloomEnabled = RuntimeJsonReaders.ReadBool(data, "bloomEnabled", true);
        o.HasBloomExtractThreshold = RuntimeJsonReaders.ReadBool(data, "hasBloomExtractThreshold", false);
        o.BloomExtractThreshold = RuntimeJsonReaders.ReadFloat(data, "bloomExtractThreshold", 1f);
        o.HasBloomExtractKnee = RuntimeJsonReaders.ReadBool(data, "hasBloomExtractKnee", false);
        o.BloomExtractKnee = RuntimeJsonReaders.ReadFloat(data, "bloomExtractKnee", 1f);

        return o;
    }

    /// <summary>Merges scene JSON onto <see cref="EngineDefaultGlobalPostProcess.DefaultSettings"/> so omitted fields (e.g. shadows) keep engine baselines.</summary>
    internal static GlobalPostProcessSettings ReadGlobalPostProcessSettings(JsonElement data)
    {
        var baseline = EngineDefaultGlobalPostProcess.DefaultSettings;
        return baseline with
        {
            BloomEnabled = RuntimeJsonReaders.ReadBool(data, "bloomEnabled", baseline.BloomEnabled),
            BloomRadius = RuntimeJsonReaders.ReadFloat(data, "bloomRadius", baseline.BloomRadius),
            BloomGain = RuntimeJsonReaders.ReadFloat(data, "bloomGain", baseline.BloomGain),
            BloomExtractThreshold = RuntimeJsonReaders.ReadFloat(data, "bloomExtractThreshold", baseline.BloomExtractThreshold),
            BloomExtractKnee = RuntimeJsonReaders.ReadFloat(data, "bloomExtractKnee", baseline.BloomExtractKnee),
            EmissiveToHdrGain = RuntimeJsonReaders.ReadFloat(data, "emissiveToHdrGain", baseline.EmissiveToHdrGain),
            BloomSourceGain = RuntimeJsonReaders.ReadFloat(data, "bloomSourceGain", baseline.BloomSourceGain),
            Exposure = RuntimeJsonReaders.ReadFloat(data, "exposure", baseline.Exposure),
            Saturation = RuntimeJsonReaders.ReadFloat(data, "saturation", baseline.Saturation),
            TonemapEnabled = RuntimeJsonReaders.ReadBool(data, "tonemapEnabled", baseline.TonemapEnabled),
            ColorGradingShadows = RuntimeJsonReaders.TryReadVec3(data, "colorGradingShadows", out var sh)
                ? sh
                : baseline.ColorGradingShadows,
            ColorGradingMidtones = RuntimeJsonReaders.TryReadVec3(data, "colorGradingMidtones", out var mid)
                ? mid
                : baseline.ColorGradingMidtones,
            ColorGradingHighlights = RuntimeJsonReaders.TryReadVec3(data, "colorGradingHighlights", out var hi)
                ? hi
                : baseline.ColorGradingHighlights,
            Shadows = ReadShadowSettings(data, baseline.Shadows),
            EmissivePromotion = ReadEmissivePromotionSettings(data, baseline.EmissivePromotion)
        };
    }

    internal static ShadowSettings ReadShadowSettings(JsonElement data, ShadowSettings baseline)
    {
        if (data.TryGetProperty("shadows", out var shadows) && shadows.ValueKind == JsonValueKind.Object)
        {
            RejectObsoleteShadowKeys(shadows);
            return baseline with
            {
                Enabled = RuntimeJsonReaders.ReadBool(shadows, "enabled", baseline.Enabled),
                SdfScale = RuntimeJsonReaders.ReadFloat(shadows, "sdfScale", baseline.SdfScale),
                ConeTraceSamples = RuntimeJsonReaders.ReadInt(shadows, "coneTraceSamples", baseline.ConeTraceSamples),
                SoftShadowK = RuntimeJsonReaders.ReadFloat(shadows, "softShadowK", baseline.SoftShadowK),
                DepthBias = RuntimeJsonReaders.ReadFloat(shadows, "depthBias", baseline.DepthBias),
                DirectionalTraceWorldDist = RuntimeJsonReaders.ReadFloat(shadows, "directionalTraceWorldDist", baseline.DirectionalTraceWorldDist),
            };
        }

        if (data.TryGetProperty("shadowsEnabled", out _))
            return baseline with { Enabled = RuntimeJsonReaders.ReadBool(data, "shadowsEnabled", baseline.Enabled) };

        return baseline;
    }

    internal static EmissivePromotionSettings ReadEmissivePromotionSettings(JsonElement data, EmissivePromotionSettings baseline)
    {
        if (data.TryGetProperty("emissivePromotion", out var promo) && promo.ValueKind == JsonValueKind.Object)
        {
            return baseline with
            {
                EmissiveLightThreshold = RuntimeJsonReaders.ReadFloat(promo, "emissiveLightThreshold", baseline.EmissiveLightThreshold),
                MaxPromotedLightsPerFrame = RuntimeJsonReaders.ReadInt(promo, "maxPromotedLightsPerFrame", baseline.MaxPromotedLightsPerFrame),
                EmissivePromotionRadiusGain = RuntimeJsonReaders.ReadFloat(promo, "emissivePromotionRadiusGain", baseline.EmissivePromotionRadiusGain),
                EmissivePromotionIntensityGain = RuntimeJsonReaders.ReadFloat(promo, "emissivePromotionIntensityGain", baseline.EmissivePromotionIntensityGain),
            };
        }

        return baseline;
    }

    /// <summary>
    /// Hard-errors on legacy shadow-atlas JSON keys so authors immediately see the breaking change instead of silently
    /// ignored properties. Per cyberland-api-evolution we do not maintain back-compat aliases.
    /// </summary>
    internal static void RejectObsoleteShadowKeys(JsonElement shadows)
    {
        var obsolete = ObsoleteShadowKeys;
        for (var i = 0; i < obsolete.Length; i++)
        {
            var key = obsolete[i];
            if (shadows.TryGetProperty(key, out _))
            {
                throw new InvalidOperationException(
                    $"Obsolete shadow JSON key '{key}': the engine now uses an SDF cone-trace pipeline. Use the new keys: sdfScale, coneTraceSamples, softShadowK, depthBias, directionalTraceWorldDist.");
            }
        }
    }

    /// <summary>Clamps negative RGB to zero and emits an <see cref="EngineDiagnostics"/> warning.</summary>
    internal static Vector3D<float> ClampLightColor(Vector3D<float> color, string lightType)
    {
        if (color.X >= 0f && color.Y >= 0f && color.Z >= 0f)
            return color;
        EngineDiagnostics.Report(
            EngineErrorSeverity.Warning,
            "Cyberland.Engine.Rendering",
            $"Negative light color on {lightType} ({color.X:F3}, {color.Y:F3}, {color.Z:F3}); clamping to zero.");
        return new Vector3D<float>(
            MathF.Max(color.X, 0f),
            MathF.Max(color.Y, 0f),
            MathF.Max(color.Z, 0f));
    }

    private static readonly string[] ObsoleteShadowKeys =
    {
        "atlasSize",
        "directionalResolution",
        "spotResolution",
        "pointResolution",
        "filterRadius",
    };
}
