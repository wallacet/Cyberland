using System.Collections.Generic;
using System.Linq;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.Scene;
using Cyberland.Engine.Scene.Systems;
using Silk.NET.Maths;
using Xunit;

namespace Cyberland.Engine.Tests;

/// <summary>
/// Integration guardrails for viewport underlines using **embedded baked MSDF atlases** (one manifest per shipped size).
/// Complements heuristic <see cref="ViewportUnderlinePlacementAssert"/> checks with quad geometry that catches strokes drawn through glyphs.
/// </summary>
public sealed class BuiltinViewportUnderlineIntegrationTests
{
    private static readonly SystemQuerySpec TextRowQuery =
        SystemQuerySpec.All<BitmapText, Transform, TextBuildFingerprint, TextSpriteCache>();

    /// <summary>Every <see cref="BuiltinFonts.BakedAtlasManifestPath"/> entry: manifest path, matching style, sample text.</summary>
    public static IEnumerable<object[]> BuiltinBakedAtlasUnderlineRows()
    {
        foreach (var row in new (string Manifest, string Family, float Size, bool Bold, string Text)[]
                 {
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular12, BuiltinFonts.UiSans, 12f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular13, BuiltinFonts.UiSans, 13f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular14, BuiltinFonts.UiSans, 14f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular15, BuiltinFonts.UiSans, 15f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular16, BuiltinFonts.UiSans, 16f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular18, BuiltinFonts.UiSans, 18f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular20, BuiltinFonts.UiSans, 20f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular22, BuiltinFonts.UiSans, 22f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular23, BuiltinFonts.UiSans, 23f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansRegular24, BuiltinFonts.UiSans, 24f, false, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansBold14, BuiltinFonts.UiSans, 14f, true, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansBold18, BuiltinFonts.UiSans, 18f, true, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.UiSansBold23, BuiltinFonts.UiSans, 23f, true, "GAME OVER"),
                     (BuiltinFonts.BakedAtlasManifestPath.MonoRegular14, BuiltinFonts.Mono, 14f, false, "0123456789 AB"),
                     (BuiltinFonts.BakedAtlasManifestPath.MonoRegular18, BuiltinFonts.Mono, 18f, false, "0123456789 AB")
                 })
            yield return new object[] { row.Manifest, row.Family, row.Size, row.Bold, row.Text };
    }

    [Theory]
    [MemberData(nameof(BuiltinBakedAtlasUnderlineRows))]
    public void TextRenderSystem_viewport_underline_builtin_atlas_does_not_cross_glyph_bodies(
        string manifestPath,
        string fontFamilyId,
        float sizePixels,
        bool bold,
        string content)
    {
        var r = new RecordingRenderer { MirrorTextGlyphsIntoSprites = false };
        r.ActiveCameraViewportSize = new Vector2D<int>(1280, 720);
        var host = new GameHostServices { Renderer = r, LocalizedContent = null };
        host.CameraRuntimeState = CameraRuntimeState.CreateDefault(r.ActiveCameraViewportSize);

        var assets = new AssetManager(new VirtualFileSystem());
        var load = host.BakedMsdfAtlasLoader.LoadFromPath(assets, host.Renderer, host.TextGlyphCache, manifestPath);
        Assert.True(load.Loaded, load.Message);

        var world = new World();
        var e = world.CreateEntity();
        world.GetOrAdd<Transform>(e) = Transform.Identity;
        ref var transform = ref world.Get<Transform>(e);
        transform.WorldPosition = new Vector2D<float>(36f, 642f);
        ref var bt = ref world.GetOrAdd<BitmapText>(e);
        bt.Visible = true;
        bt.Content = content;
        bt.IsLocalizationKey = false;
        bt.CoordinateSpace = CoordinateSpace.ViewportSpace;
        bt.SortKey = 450f;
        bt.Style = new TextStyle(fontFamilyId, sizePixels, new Vector4D<float>(1f, 0.42f, 0.48f, 1f), Bold: bold,
            Underline: true);

        var sys = new TextRenderSystem(host);
        var q = world.QueryChunks(TextRowQuery);
        sys.OnStart(world, q);
        sys.OnLateUpdate(q, 0.016f);

        Assert.NotEmpty(r.TextGlyphs);
        var underline = Assert.Single(r.Sprites);
        var rowSortKey = bt.SortKey;
        var rowGlyphs = r.TextGlyphs.Where(g =>
            g.Space == CoordinateSpace.ViewportSpace && Math.Abs(g.SortKey - rowSortKey) < 1e-4f).ToList();
        Assert.NotEmpty(rowGlyphs);

        var baselineSnapped = MathF.Round(world.Get<TextSpriteCache>(e).BaselineAuthored.Y);
        ViewportUnderlineGlyphBodyAssert.UnderlineMustNotOverlapBaselineClippedGlyphInk(in underline, rowGlyphs,
            baselineSnapped, sizePixels);

        var inkMinTop = rowGlyphs.Min(g => g.Center.Y - g.HalfExtents.Y);
        var inkMaxBottom = rowGlyphs.Max(g => g.Center.Y + g.HalfExtents.Y);
        ViewportUnderlinePlacementAssert.FollowsTightVisibleBandRules(in underline, baselineSnapped, sizePixels, inkMinTop,
            inkMaxBottom);
    }
}
