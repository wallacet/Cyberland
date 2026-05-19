using System.Text.Json;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Engine.Tests;

public sealed class UiRuntimeCoverageTests
{
    [Fact]
    public async Task UiRuntime_All_layout_presets_and_texture_aliases()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  { "type": "cyberland.engine/panel", "layout": { "preset": "stretchWidthAutoHeight" } },
                  { "type": "cyberland.engine/panel", "layout": { "preset": "centerFixed", "width": 10, "height": 10 } },
                  { "type": "cyberland.engine/panel", "layout": { "preset": "bottomRightFixed", "width": 8, "height": 8, "margin": 4 } },
                  {
                    "type": "cyberland.engine/panel",
                    "backgroundTexture": "white",
                    "layout": { "preset": "topLeftFixed", "width": 4, "height": 4 }
                  },
                  {
                    "type": "cyberland.engine/image",
                    "sourceTexture": "defaultNormal",
                    "layout": { "preset": "topLeftFixed", "width": 4, "height": 4 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public async Task UiRuntime_allow_unknown_element_types()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [ { "type": "cyberland.demo/missing" } ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path, new UiLoadOptions { AllowUnknownElementTypes = true });
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task UiAttachResult_reports_build_failure()
    {
        var vfs = new VirtualFileSystem();
        var ui = CreateUiRuntime(vfs);
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var attach = await ui.AttachToEntityAsync(new EntityId(1), "Ui/nope.json", host);
        Assert.False(attach.Succeeded);
        Assert.NotNull(attach.Build);
        Assert.NotNull(attach.ErrorMessage);
    }

    [Fact]
    public void UiBuildResult_empty_elements_when_no_session()
    {
        var r = new UiBuildResult { Succeeded = false };
        Assert.Empty(r.ElementsById);
    }

    [Fact]
    public void UiBuildSession_GetOrCreateRadioGroup_reuses_group()
    {
        var s = new UiBuildSession();
        var a = s.GetOrCreateRadioGroup("g");
        var b = s.GetOrCreateRadioGroup("g");
        Assert.Same(a, b);
        Assert.True(s.TryGetRadioGroup("g", out var g));
        Assert.Same(a, g);
    }

    [Fact]
    public async Task UiRuntime_missing_migrator_fails()
    {
        var (vfs, path) = await WriteUiAsync("""{"schemaVersion":0,"root":{"type":"cyberland.engine/panel"}}""");
        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.False(result.Succeeded);
        Assert.Contains("migrator", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UiRuntime_unsupported_future_schema_fails()
    {
        var (vfs, path) = await WriteUiAsync("""{"schemaVersion":99,"root":{"type":"cyberland.engine/panel"}}""");
        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UiRuntime_Register_and_SetRenderer_validate()
    {
        var vfs = new VirtualFileSystem();
        var ui = new UiRuntime(vfs, () => null);
        Assert.Throws<ArgumentNullException>(() => ui.RegisterElementDeserializer(null!, static (in UiElementDeserializeContext _) => new UiPanel()));
        Assert.Throws<ArgumentNullException>(() => ui.RegisterElementDeserializer("x", null!));
        Assert.Throws<ArgumentNullException>(() => ui.RegisterSchemaMigrator(0, 1, null!));
        Assert.Throws<ArgumentNullException>(() => ui.SetRenderer(null!));
        Assert.Throws<InvalidOperationException>(() => ui.LoadDocumentAsync("x").AsTask().GetAwaiter().GetResult());
        Assert.Throws<ArgumentNullException>(() => ui.AttachToEntityAsync(default, "x", null!).AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public void UiDocumentRegistry_TryGetElements_and_prune()
    {
        var reg = new UiDocumentRegistry();
        var doc = new UiDocument();
        var e = new EntityId(7);
        reg.Register(e, doc, new Dictionary<string, UiElement> { ["a"] = doc.Root });
        Assert.True(reg.TryGetElements(e, out var map));
        Assert.True(map.ContainsKey("a"));
        reg.PruneToEntities(ReadOnlySpan<EntityId>.Empty);
        Assert.False(reg.TryGet(e, out _));
    }

    [Fact]
    public async Task UiRuntime_shared_fields_and_typed_overrides()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "visible": false,
                "interactable": true,
                "sortKey": 2,
                "clipMode": "IntersectParent",
                "margin": { "left": 1, "top": 2, "right": 3, "bottom": 4 },
                "padding": { "left": 5, "top": 6, "right": 7, "bottom": 8 },
                "anchorMin": { "x": 0.1, "y": 0.2 },
                "anchorMax": { "x": 0.9, "y": 0.8 },
                "pivot": { "x": 0.5, "y": 0.5 },
                "anchoredPosition": { "x": 3, "y": 4 },
                "sizeDelta": { "x": 10, "y": 20 },
                "stretch": { "left": 1, "right": 2, "top": 3, "bottom": 4 },
                "layout": { "preset": "topStretch", "height": 40, "anchoredPosition": { "x": 9, "y": 8 } },
                "children": [
                  {
                    "type": "cyberland.engine/scroll-view",
                    "wheelScrollPixels": 12,
                    "layout": { "preset": "topStretch", "height": 30 },
                    "content": { "type": "cyberland.engine/panel" }
                  },
                  {
                    "type": "cyberland.engine/image",
                    "tint": { "x": 0.2, "y": 0.3, "z": 0.4, "w": 0.5 },
                    "sourceTexture": "white",
                    "layout": { "preset": "topLeftFixed", "width": 8, "height": 8 }
                  },
                  {
                    "type": "cyberland.engine/button",
                    "normalBackground": { "x": 0.1, "y": 0.1, "z": 0.1, "w": 1 },
                    "pressedBackground": { "x": 0.2, "y": 0.2, "z": 0.2, "w": 1 },
                    "layout": { "preset": "topLeftFixed", "width": 40, "height": 24 }
                  },
                  {
                    "type": "cyberland.engine/label",
                    "locKey": "k",
                    "backgroundColor": { "x": 0, "y": 0, "z": 0, "w": 1 },
                    "sizePixels": 12,
                    "layout": { "preset": "topLeftFixed", "width": 40, "height": 20 }
                  },
                  {
                    "type": "cyberland.engine/radio-button",
                    "groupId": "g",
                    "optionId": "a",
                    "normalTint": { "x": 0.1, "y": 0.1, "z": 0.1, "w": 1 },
                    "selectedTint": { "x": 0.2, "y": 0.2, "z": 0.2, "w": 1 },
                    "width": 30,
                    "height": 20,
                    "layout": { "preset": "topLeftFixed", "width": 30, "height": 20 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        Assert.True((await ui.LoadDocumentAsync(path)).Succeeded);
    }

    [Fact]
    public async Task UiRuntime_schema_migrator_bumps_version()
    {
        var (vfs, path) = await WriteUiAsync("""{"schemaVersion":0,"root":{"type":"cyberland.engine/panel"}}""");
        var ui = CreateUiRuntime(vfs);
        ui.RegisterSchemaMigrator(0, 1, static el =>
        {
            using var doc = JsonDocument.Parse(el.GetRawText());
            using var ms = new MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                w.WriteNumber("schemaVersion", 1);
                w.WritePropertyName("root");
                doc.RootElement.GetProperty("root").WriteTo(w);
                w.WriteEndObject();
            }

            return JsonDocument.Parse(ms.ToArray()).RootElement;
        });
        Assert.True((await ui.LoadDocumentAsync(path)).Succeeded);
    }

    [Fact]
    public void UiElementLookup_wrong_type_and_missing_id()
    {
        var map = new Dictionary<string, UiElement> { ["b"] = new UiButton() };
        Assert.Throws<InvalidOperationException>(() => map.Require<UiTextBlock>("b"));
        Assert.Throws<InvalidOperationException>(() => map.Require<UiButton>("missing"));
        Assert.False(map.TryGet<UiTextBlock>("b", out _));
        Assert.False(map.TryGet<UiTextBlock>("missing", out _));
    }

    [Fact]
    public void UiRuntime_custom_deserializer_reads_context()
    {
        var vfs = new VirtualFileSystem();
        var ui = new UiRuntime(vfs, () => null);
        ui.SetRenderer(new RecordingRenderer());
        var doc = new UiDocument();
        var session = new UiBuildSession();
        using var json = JsonDocument.Parse("""{"type":"test/custom"}""");
        var node = json.RootElement;
        var renderer = new RecordingRenderer();
        var ctx = new UiElementDeserializeContext(node, doc, session, renderer, null);
        Assert.Equal(node.GetRawText(), ctx.Node.GetRawText());
        Assert.Same(doc, ctx.Document);
        Assert.Same(session, ctx.Session);
        Assert.Same(renderer, ctx.Renderer);
        Assert.Null(ctx.Strings);
        ui.RegisterElementDeserializer("test/custom", static (in UiElementDeserializeContext c) => new UiPanel());
        Assert.NotNull(ui);
    }

    [Fact]
    public async Task UiRuntime_edge_parse_and_scroll_without_content()
    {
        var (vfs, badRoot) = await WriteUiAsync("""{"schemaVersion":1,"root":[]}""");
        var ui = CreateUiRuntime(vfs);
        Assert.False((await ui.LoadDocumentAsync(badRoot)).Succeeded);

        var (vfs2, path) = await WriteUiAsync(
            """
            {"schemaVersion":1,"root":{"type":"cyberland.engine/scroll-view","layout":{"preset":"topStretch","height":20},"content":[]}}
            """);
        Assert.True((await CreateUiRuntime(vfs2).LoadDocumentAsync(path)).Succeeded);

        var (vfs2b, path2b) = await WriteUiAsync(
            """
            {"schemaVersion":1,"root":{"type":"cyberland.engine/scroll-view","layout":{"preset":"topStretch","height":20}}}
            """);
        Assert.True((await CreateUiRuntime(vfs2b).LoadDocumentAsync(path2b)).Succeeded);

        var (vfs3, path3) = await WriteUiAsync(
            """
            {"schemaVersion":1,"root":{"type":"cyberland.engine/panel","backgroundTexture":"bogus","children":[{"type":"cyberland.engine/vertical-stack","backgroundColor":{"x":1,"y":0,"z":0,"w":1}}]}}
            """);
        Assert.True((await CreateUiRuntime(vfs3).LoadDocumentAsync(path3)).Succeeded);

        var el = new UiPanel();
        using (var emptyPreset = JsonDocument.Parse("""{"layout":{"preset":""}}"""))
            EngineUiElementDeserializers.ApplySharedElementFields(el, emptyPreset.RootElement, new UiBuildSession());

        var (vfs4, path4) = await WriteUiAsync(
            """
            {"schemaVersion":1,"root":{"type":"cyberland.engine/image","sourceTexture":"notATexture","layout":{"preset":"topRightFixed","width":8,"height":8,"margin":2,"anchoredPosition":{"x":1,"y":2}}}}
            """);
        Assert.True((await CreateUiRuntime(vfs4).LoadDocumentAsync(path4)).Succeeded);

        var (vfs5, path5) = await WriteUiAsync(
            """{"schemaVersion":1,"root":{"type":"cyberland.engine/panel","children":[{}]}}""");
        Assert.False((await CreateUiRuntime(vfs5).LoadDocumentAsync(path5)).Succeeded);
    }

    [Fact]
    public void BuildScrollViewContent_missing_or_invalid_content_is_noop()
    {
        var scroll = new UiScrollView();
        var uiDoc = new UiDocument();
        var session = new UiBuildSession();
        var renderer = new RecordingRenderer();
        var ui = CreateUiRuntime(new VirtualFileSystem());
        var map = GetDeserializers(ui);

        using (var noContent = JsonDocument.Parse("{}"))
            EngineUiElementDeserializers.BuildScrollViewContent(
                scroll, noContent.RootElement, uiDoc, session, renderer, null, map, false);

        using (var badContent = JsonDocument.Parse("""{"content":"not-an-object"}"""))
            EngineUiElementDeserializers.BuildScrollViewContent(
                scroll, badContent.RootElement, uiDoc, session, renderer, null, map, false);
    }

    [Fact]
    public void ReadBuiltinTexture_unknown_name_returns_fallback()
    {
        var renderer = new RecordingRenderer();
        using var json = JsonDocument.Parse("""{"backgroundTexture":"unknown-alias"}""");
        var fallback = uint.MaxValue;
        var id = InvokeReadBuiltinTexture(json.RootElement, "backgroundTexture", renderer, fallback);
        Assert.Equal(fallback, id);
    }

    private static uint InvokeReadBuiltinTexture(JsonElement node, string name, IRenderer renderer, uint fallback)
    {
        var method = typeof(EngineUiElementDeserializers).GetMethod(
            "ReadBuiltinTexture",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (uint)method.Invoke(null, [node, name, renderer, fallback])!;
    }

    [Fact]
    public void BuildScrollViewContent_clears_existing_content_children()
    {
        var scroll = new UiScrollView();
        scroll.Content.AddChild(new UiPanel());
        using var json = JsonDocument.Parse(
            """{"content":{"type":"cyberland.engine/panel","id":"inner"}}""");
        var uiDoc = new UiDocument();
        var session = new UiBuildSession();
        var renderer = new RecordingRenderer();
        var ui = CreateUiRuntime(new VirtualFileSystem());
        EngineUiElementDeserializers.BuildScrollViewContent(
            scroll, json.RootElement, uiDoc, session, renderer, null, GetDeserializers(ui), false);
        Assert.Single(scroll.Content.Children);
        Assert.True(session.ElementsById.ContainsKey("inner"));
    }

    private static IReadOnlyDictionary<string, UiElementDeserializer> GetDeserializers(UiRuntime ui)
    {
        var field = typeof(UiRuntime).GetField("_deserializers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (IReadOnlyDictionary<string, UiElementDeserializer>)field.GetValue(ui)!;
    }

    [Fact]
    public void UiElementLookup_TryGet_success()
    {
        var map = new Dictionary<string, UiElement> { ["b"] = new UiButton() };
        Assert.True(map.TryGet<UiButton>("b", out var btn));
        Assert.NotNull(btn);
    }

    [Fact]
    public void GameHostServices_InitializeRuntimeUi_validates()
    {
        var host = new GameHostServices();
        Assert.Throws<ArgumentNullException>(() => host.InitializeRuntimeUi(null!, new RecordingRenderer(), () => null));
        Assert.Throws<ArgumentNullException>(() => host.InitializeRuntimeUi(new VirtualFileSystem(), null!, () => null));
        Assert.Throws<ArgumentNullException>(() => host.InitializeRuntimeUi(new VirtualFileSystem(), new RecordingRenderer(), null!));
    }

    private static UiRuntime CreateUiRuntime(VirtualFileSystem vfs)
    {
        var ui = new UiRuntime(vfs, () => null);
        ui.SetRenderer(new RecordingRenderer());
        return ui;
    }

    private static async Task<(VirtualFileSystem Vfs, string Path)> WriteUiAsync(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_ui_cov_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Ui"));
        var rel = "Content/Ui/doc.json";
        await File.WriteAllTextAsync(Path.Combine(dir, rel), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        return (vfs, rel);
    }
}
