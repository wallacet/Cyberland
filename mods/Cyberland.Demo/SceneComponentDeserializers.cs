using System.Text.Json;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Scene;
using Silk.NET.Maths;

namespace Cyberland.Demo;

/// <summary>
/// Scene JSON component types for the HDR tutorial (<c>Content/Scenes/demo_hdr.json</c>).
/// </summary>
public static class SceneComponentDeserializers
{
    /// <summary>Registers all <c>cyberland.demo/*</c> and extended engine payloads used by the HDR scene file.</summary>
    public static void Register(ISceneRuntime scenes, GameHostServices host)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        ArgumentNullException.ThrowIfNull(host);
        var renderer = host.Renderer ?? throw new InvalidOperationException("Renderer is required for HDR scene deserialization.");
        var white = renderer.WhiteTextureId;
        var defaultNormal = renderer.DefaultNormalTextureId;

        scenes.RegisterComponentDeserializer("cyberland.demo/player-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<PlayerTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/background-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<BackgroundTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/neon-strip-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<NeonStripTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-title-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudTitleTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-hint-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudHintTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hud-fps-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HudFpsTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-warm-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrWarmPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-player-point-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrPlayerPointTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/hdr-bloom-volume-tag", static (in SceneComponentDeserializeContext ctx) =>
            ctx.World.GetOrAdd<HdrBloomVolumeTag>(ctx.EntityId));

        scenes.RegisterComponentDeserializer("cyberland.demo/velocity", static (in SceneComponentDeserializeContext ctx) =>
        {
            _ = ctx.World.GetOrAdd<Velocity>(ctx.EntityId);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/camera2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            var w = SceneJson.ReadInt(ctx.Data, "viewportWidth", 1280);
            var h = SceneJson.ReadInt(ctx.Data, "viewportHeight", 720);
            ctx.World.GetOrAdd<Camera2D>(ctx.EntityId) = Camera2D.Create(new Vector2D<int>(w, h));
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/sprite", (in SceneComponentDeserializeContext ctx) =>
        {
            ref var sprite = ref ctx.World.GetOrAdd<Sprite>(ctx.EntityId);
            var half = SceneJson.ReadFloat(ctx.Data, "halfExtent", Constants.SpriteHalfExtent);
            sprite = Sprite.DefaultWhiteUnlit(white, defaultNormal, new Vector2D<float>(half, half));
            sprite.Layer = SceneJson.ReadInt(ctx.Data, "layer", (int)SpriteLayer.World);
            sprite.SortKey = SceneJson.ReadFloat(ctx.Data, "sortKey", 0f);
            sprite.Visible = SceneJson.ReadBool(ctx.Data, "visible", true);
            if (SceneJson.TryReadVec2(ctx.Data, "halfExtents", out var he))
                sprite.HalfExtents = he;
            if (SceneJson.TryReadVec4(ctx.Data, "colorMultiply", out var cm))
                sprite.ColorMultiply = cm;
            if (SceneJson.TryReadVec3(ctx.Data, "emissiveTint", out var et))
                sprite.EmissiveTint = et;
            sprite.EmissiveIntensity = SceneJson.ReadFloat(ctx.Data, "emissiveIntensity", sprite.EmissiveIntensity);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/viewport-anchor-2d", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var anchor = ref ctx.World.GetOrAdd<ViewportAnchor2D>(ctx.EntityId);
            anchor.Active = SceneJson.ReadBool(ctx.Data, "active", true);
            anchor.ContentSpace = SceneJson.ReadEnum(ctx.Data, "contentSpace", CoordinateSpace.WorldSpace);
            anchor.Anchor = SceneJson.ReadEnum(ctx.Data, "anchor", ViewportAnchorPreset.Center);
            anchor.OffsetX = SceneJson.ReadFloat(ctx.Data, "offsetX", 0f);
            anchor.OffsetY = SceneJson.ReadFloat(ctx.Data, "offsetY", 0f);
            anchor.SyncSpriteHalfExtentsToViewport = SceneJson.ReadBool(ctx.Data, "syncSpriteHalfExtentsToViewport", false);
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/bitmap-text", static (in SceneComponentDeserializeContext ctx) =>
        {
            ref var text = ref ctx.World.GetOrAdd<BitmapText>(ctx.EntityId);
            text.Visible = SceneJson.ReadBool(ctx.Data, "visible", true);
            text.IsLocalizationKey = SceneJson.ReadBool(ctx.Data, "isLocalizationKey", false);
            text.Content = SceneJson.ReadString(ctx.Data, "content") ?? "";
            text.SortKey = SceneJson.ReadFloat(ctx.Data, "sortKey", 0f);
            text.CoordinateSpace = SceneJson.ReadEnum(ctx.Data, "coordinateSpace", BitmapText.HudDefaultCoordinateSpace);
            var family = SceneJson.ReadString(ctx.Data, "fontFamily") ?? BuiltinFonts.UiSans;
            var size = SceneJson.ReadFloat(ctx.Data, "sizePixels", 16f);
            var color = SceneJson.TryReadVec4(ctx.Data, "color", out var c)
                ? c
                : new Vector4D<float>(1f, 1f, 1f, 1f);
            text.Style = new TextStyle(
                family,
                size,
                color,
                SceneJson.ReadBool(ctx.Data, "bold", false),
                SceneJson.ReadBool(ctx.Data, "italic", false));
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/ambient-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<AmbientLightSource>(ctx.EntityId) = new AmbientLightSource
            {
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Color = SceneJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneJson.ReadFloat(ctx.Data, "intensity", 1f)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/directional-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<DirectionalLightSource>(ctx.EntityId) = new DirectionalLightSource
            {
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Color = SceneJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneJson.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = SceneJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/spot-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<SpotLightSource>(ctx.EntityId) = new SpotLightSource
            {
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Radius = SceneJson.ReadFloat(ctx.Data, "radius", 100f),
                InnerConeRadians = SceneJson.ReadFloat(ctx.Data, "innerConeRadians", MathF.PI / 4f),
                OuterConeRadians = SceneJson.ReadFloat(ctx.Data, "outerConeRadians", MathF.PI / 2f),
                Color = SceneJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneJson.ReadFloat(ctx.Data, "intensity", 1f),
                CastsShadow = SceneJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/point-light", static (in SceneComponentDeserializeContext ctx) =>
        {
            ctx.World.GetOrAdd<PointLightSource>(ctx.EntityId) = new PointLightSource
            {
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Radius = SceneJson.ReadFloat(ctx.Data, "radius", 100f),
                Color = SceneJson.TryReadVec3(ctx.Data, "color", out var col) ? col : Vector3D<float>.One,
                Intensity = SceneJson.ReadFloat(ctx.Data, "intensity", 1f),
                FalloffExponent = SceneJson.ReadFloat(ctx.Data, "falloffExponent", 2f),
                CastsShadow = SceneJson.ReadBool(ctx.Data, "castsShadow", false)
            };
        });

        scenes.RegisterComponentDeserializer("cyberland.engine/post-process-volume", static (in SceneComponentDeserializeContext ctx) =>
        {
            var halfW = SceneJson.ReadFloat(ctx.Data, "halfExtentsX", 640f);
            var halfH = SceneJson.ReadFloat(ctx.Data, "halfExtentsY", 360f);
            ctx.World.GetOrAdd<PostProcessVolumeSource>(ctx.EntityId) = new PostProcessVolumeSource
            {
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Volume = new PostProcessVolume
                {
                    HalfExtentsLocal = new Vector2D<float>(halfW, halfH),
                    Priority = SceneJson.ReadInt(ctx.Data, "priority", 0),
                    Overrides = new PostProcessOverrides
                    {
                        HasBloomGain = SceneJson.ReadBool(ctx.Data, "hasBloomGain", false),
                        BloomGain = SceneJson.ReadFloat(ctx.Data, "bloomGain", 1f),
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
                Active = SceneJson.ReadBool(ctx.Data, "active", true),
                Priority = SceneJson.ReadInt(ctx.Data, "priority", 0),
                Settings = new GlobalPostProcessSettings
                {
                    BloomEnabled = SceneJson.ReadBool(ctx.Data, "bloomEnabled", true),
                    BloomRadius = SceneJson.ReadFloat(ctx.Data, "bloomRadius", 1f),
                    BloomGain = SceneJson.ReadFloat(ctx.Data, "bloomGain", 1f),
                    BloomExtractThreshold = SceneJson.ReadFloat(ctx.Data, "bloomExtractThreshold", 0.5f),
                    BloomExtractKnee = SceneJson.ReadFloat(ctx.Data, "bloomExtractKnee", 0.5f),
                    EmissiveToHdrGain = SceneJson.ReadFloat(ctx.Data, "emissiveToHdrGain", 0.5f),
                    EmissiveToBloomGain = SceneJson.ReadFloat(ctx.Data, "emissiveToBloomGain", 0.5f),
                    Exposure = SceneJson.ReadFloat(ctx.Data, "exposure", 1f),
                    Saturation = SceneJson.ReadFloat(ctx.Data, "saturation", 1f),
                    TonemapEnabled = SceneJson.ReadBool(ctx.Data, "tonemapEnabled", true),
                    ColorGradingShadows = SceneJson.TryReadVec3(ctx.Data, "colorGradingShadows", out var sh) ? sh : Vector3D<float>.One,
                    ColorGradingMidtones = SceneJson.TryReadVec3(ctx.Data, "colorGradingMidtones", out var mid) ? mid : Vector3D<float>.One,
                    ColorGradingHighlights = SceneJson.TryReadVec3(ctx.Data, "colorGradingHighlights", out var hi) ? hi : Vector3D<float>.One
                }
            };
        });
    }

    private static class SceneJson
    {
        public static float ReadFloat(JsonElement data, string name, float fallback)
        {
            if (data.TryGetProperty(name, out var p) && p.TryGetSingle(out var v))
                return v;
            return fallback;
        }

        public static int ReadInt(JsonElement data, string name, int fallback)
        {
            if (data.TryGetProperty(name, out var p) && p.TryGetInt32(out var v))
                return v;
            return fallback;
        }

        public static bool ReadBool(JsonElement data, string name, bool fallback)
        {
            if (data.TryGetProperty(name, out var p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False))
                return p.GetBoolean();
            return fallback;
        }

        public static string? ReadString(JsonElement data, string name)
        {
            if (data.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            return null;
        }

        public static TEnum ReadEnum<TEnum>(JsonElement data, string name, TEnum fallback)
            where TEnum : struct, Enum
        {
            var s = ReadString(data, name);
            return s is not null && Enum.TryParse<TEnum>(s, ignoreCase: true, out var e) ? e : fallback;
        }

        public static bool TryReadVec2(JsonElement data, string name, out Vector2D<float> value)
        {
            value = default;
            if (!data.TryGetProperty(name, out var obj))
                return false;
            var x = ReadFloat(obj, "x", 0f);
            var y = ReadFloat(obj, "y", 0f);
            value = new Vector2D<float>(x, y);
            return true;
        }

        public static bool TryReadVec3(JsonElement data, string name, out Vector3D<float> value)
        {
            value = default;
            if (!data.TryGetProperty(name, out var obj))
                return false;
            value = new Vector3D<float>(
                ReadFloat(obj, "x", 0f),
                ReadFloat(obj, "y", 0f),
                ReadFloat(obj, "z", 0f));
            return true;
        }

        public static bool TryReadVec4(JsonElement data, string name, out Vector4D<float> value)
        {
            value = default;
            if (!data.TryGetProperty(name, out var obj))
                return false;
            value = new Vector4D<float>(
                ReadFloat(obj, "x", 0f),
                ReadFloat(obj, "y", 0f),
                ReadFloat(obj, "z", 0f),
                ReadFloat(obj, "w", 1f));
            return true;
        }
    }
}
