using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Core.Tasks;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.Rendering.Text;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeUi;
using Cyberland.Engine.Serialization;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;
using Cyberland.Engine.UI.Ecs;
using Cyberland.Engine.UI.Layout;
using Cyberland.Engine.UI.Text;

namespace Cyberland.Engine.Tests;

public sealed class UiRuntimeTests
{
    private const string SampleText =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123456789";

    [Fact]
    public void RuntimeJsonReaders_ReadThickness_parses_edges()
    {
        using var doc = JsonDocument.Parse("""{"margin":{"left":1,"top":2,"right":3,"bottom":4}}""");
        var t = RuntimeJsonReaders.ReadThickness(doc.RootElement, "margin", default);
        Assert.Equal(1f, t.Left);
        Assert.Equal(4f, t.Bottom);
    }

    [Fact]
    public async Task UiRuntime_loads_minimal_tree_and_registers_ids()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/vertical-stack",
                "id": "root",
                "layout": { "preset": "stretchAll" },
                "children": [
                  {
                    "type": "cyberland.engine/button",
                    "id": "btn.ok",
                    "layout": { "preset": "topStretch", "height": 32 },
                    "normalBackground": { "x": 0.2, "y": 0.2, "z": 0.3, "w": 1 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.Document);
        var btn = result.ElementsById.Require<UiButton>("btn.ok");
        Assert.True(btn.Interactable);
    }

    [Fact]
    public async Task UiRuntime_duplicate_id_fails()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "id": "a",
                "children": [
                  { "type": "cyberland.engine/panel", "id": "a" }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.False(result.Succeeded);
        Assert.Contains("Duplicate", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiRuntime_unknown_type_fails_by_default()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  { "type": "cyberland.demo/unknown" }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UiRuntime_text_block_fontFamily_shorthand_resolves_builtin_ids()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "sans",
                    "text": "A",
                    "fontFamily": "UiSans",
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 24 }
                  },
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "mono",
                    "text": "B",
                    "fontFamily": "Mono",
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 24 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(BuiltinFonts.UiSans, result.ElementsById.Require<UiTextBlock>("sans").DefaultStyle.FontFamilyId);
        Assert.Equal(BuiltinFonts.Mono, result.ElementsById.Require<UiTextBlock>("mono").DefaultStyle.FontFamilyId);
    }

    [Fact]
    public async Task UiRuntime_text_block_locKey_with_isLocalizationKey_builds_runs()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "t",
                    "locKey": "greeting",
                    "isLocalizationKey": true,
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 24 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var block = result.ElementsById.Require<UiTextBlock>("t");
        Assert.NotNull(block.Runs);
        Assert.Single(block.Runs);
        Assert.True(block.Runs[0].IsLocalizationKey);
        Assert.Equal("greeting", block.Runs[0].Content);
    }

    [Fact]
    public async Task UiRuntime_text_block_literal_with_isLocalizationKey_uses_text_as_key()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "t",
                    "text": "farewell",
                    "isLocalizationKey": true,
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 24 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var block = result.ElementsById.Require<UiTextBlock>("t");
        Assert.NotNull(block.Runs);
        Assert.Equal("farewell", block.Runs[0].Content);
    }

    [Fact]
    public async Task UiRuntime_text_block_isLocalizationKey_without_key_leaves_runs_null()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "t",
                    "isLocalizationKey": true,
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 24 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var block = result.ElementsById.Require<UiTextBlock>("t");
        Assert.Null(block.Runs);
    }

    [Fact]
    public async Task UiRuntime_element_slugs_smoke_layout()
    {
        var (vfs, path) = await WriteUiAsync(
            $$"""
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/vertical-stack",
                "id": "root",
                "layout": { "preset": "stretchAll" },
                "children": [
                  {
                    "type": "cyberland.engine/horizontal-stack",
                    "id": "row",
                    "spacing": 4,
                    "crossAlignment": "Stretch",
                    "layout": { "preset": "topStretch", "height": 40 },
                    "children": [
                      {
                        "type": "cyberland.engine/grid",
                        "id": "grid",
                        "columnCount": 2,
                        "spacing": 2,
                        "layout": { "preset": "topLeftFixed", "width": 80, "height": 40 },
                        "children": [
                          { "type": "cyberland.engine/image", "id": "img", "sourceTexture": "white", "layout": { "preset": "topLeftFixed", "width": 20, "height": 20 } },
                          { "type": "cyberland.engine/label", "id": "lbl", "text": "L", "sizePixels": 12, "layout": { "preset": "topLeftFixed", "width": 40, "height": 20 } }
                        ]
                      }
                    ]
                  },
                  {
                    "type": "cyberland.engine/scroll-view",
                    "id": "scroll",
                    "wheelScrollPixels": 24,
                    "layout": { "preset": "topStretch", "height": 60 },
                    "content": {
                      "type": "cyberland.engine/vertical-stack",
                      "id": "scroll.body",
                      "children": [
                        {
                          "type": "cyberland.engine/text-block",
                          "id": "body",
                          "text": "{{SampleText}}",
                          "sizePixels": 14,
                          "layout": { "preset": "topStretch", "height": 28 }
                        }
                      ]
                    }
                  },
                  {
                    "type": "cyberland.engine/radio-button",
                    "id": "r.a",
                    "groupId": "g",
                    "optionId": "a",
                    "width": 40,
                    "height": 24,
                    "layout": { "preset": "topLeftFixed", "width": 40, "height": 24 }
                  },
                  {
                    "type": "cyberland.engine/radio-button",
                    "id": "r.b",
                    "groupId": "g",
                    "optionId": "b",
                    "width": 40,
                    "height": 24,
                    "layout": { "preset": "topLeftFixed", "width": 40, "height": 24 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.Session!.TryGetRadioGroup("g", out var group));
        Assert.NotNull(group);
        result.ElementsById.Require<UiScrollView>("scroll");
        result.ElementsById.Require<UiTextBlock>("body");
        result.Document!.MeasureArrange(new Silk.NET.Maths.Vector2D<float>(400f, 300f));
    }

    [Fact]
    public async Task SceneRuntime_uiPath_attaches_document()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_ui_attach_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Ui"));
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Ui", "hud.json"),
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "id": "hud.panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "hud.score",
                    "text": "Score",
                    "sizePixels": 18,
                    "layout": { "preset": "topStretch", "height": 24 }
                  }
                ]
              }
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "scene.json"),
            """
            {"schemaVersion":1,"entities":[{"logicalId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","components":[
              {"type":"cyberland.engine/ui-document-root","data":{"uiPath":"Content/Ui/hud.json","sortKeyBase":850}}
            ]}]}
            """);

        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));
        host.InitializeRuntimeUi(vfs, host.Renderer, () => null);

        var spawn = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/scene.json");
        Assert.True(spawn.Succeeded, spawn.ErrorMessage);

        var eid = LogicalActorLookup.Resolve(world, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Assert.True(host.UiDocuments.TryGet(eid, out var doc));
        Assert.NotNull(doc);
        var panel = doc!.Root;
        Assert.Single(panel.Children);
        var score = Assert.IsType<UiTextBlock>(panel.Children[0]);
        Assert.Equal("Score", score.Text);
    }

    [Fact]
    public async Task SceneRuntime_uiPath_attach_failure_fails_spawn()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_ui_badattach_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Ui"));
        await File.WriteAllTextAsync(Path.Combine(dir, "Content", "Ui", "bad.json"), "{ not json");
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "scene.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/ui-document-root","data":{"uiPath":"Content/Ui/bad.json"}}
            ]}]}
            """);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));
        host.InitializeRuntimeUi(vfs, host.Renderer, () => null);
        var spawn = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/scene.json");
        Assert.False(spawn.Succeeded);
    }

    [Fact]
    public async Task SceneRuntime_uiPath_without_runtime_fails()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_ui_noui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Scenes"));
        await File.WriteAllTextAsync(
            Path.Combine(dir, "Content", "Scenes", "scene.json"),
            """
            {"schemaVersion":1,"entities":[{"components":[
              {"type":"cyberland.engine/ui-document-root","data":{"uiPath":"Ui/missing.json"}}
            ]}]}
            """);

        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        var host = new GameHostServices { Renderer = new RecordingRenderer() };
        var world = new World();
        host.InitializeRuntimeScenes(vfs, new ParallelismSettings(), () => null, world, new SystemScheduler(new ParallelismSettings()));

        var spawn = await host.RuntimeScenes!.SpawnIntoWorldAsync(world, "Content/Scenes/scene.json");
        Assert.False(spawn.Succeeded);
        Assert.Contains("UI runtime", spawn.ErrorMessage ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void EngineUiElementDeserializers_Register_validates_arguments()
    {
        var vfs = new VirtualFileSystem();
        var ui = new UiRuntime(vfs, () => null);
        var renderer = new RecordingRenderer();
        Assert.Throws<ArgumentNullException>(() => EngineUiElementDeserializers.Register(null!, renderer));
        Assert.Throws<ArgumentNullException>(() => EngineUiElementDeserializers.Register(ui, null!));
    }

    [Fact]
    public async Task UiRuntime_text_block_isLocalizationKey_sets_runs()
    {
        var (vfs, path) = await WriteUiAsync(
            """
            {
              "schemaVersion": 1,
              "root": {
                "type": "cyberland.engine/panel",
                "children": [
                  {
                    "type": "cyberland.engine/text-block",
                    "id": "t",
                    "locKey": "demo.key",
                    "isLocalizationKey": true,
                    "sizePixels": 14,
                    "layout": { "preset": "topStretch", "height": 20 }
                  }
                ]
              }
            }
            """);

        var ui = CreateUiRuntime(vfs);
        var result = await ui.LoadDocumentAsync(path);
        Assert.True(result.Succeeded, result.ErrorMessage);
        var tb = result.ElementsById.Require<UiTextBlock>("t");
        Assert.NotNull(tb.Runs);
        Assert.Single(tb.Runs);
        Assert.True(tb.Runs[0].IsLocalizationKey);
    }

    [Fact]
    public void UiElementLookup_Require_and_TryGet_enforce_types()
    {
        var map = new Dictionary<string, UiElement> { ["x"] = new UiButton() };
        Assert.Throws<InvalidOperationException>(() => map.Require<UiTextBlock>("x"));
        Assert.False(map.TryGet<UiTextBlock>("x", out _));
        _ = map.Require<UiButton>("x");
    }

    private static UiRuntime CreateUiRuntime(VirtualFileSystem vfs)
    {
        var ui = new UiRuntime(vfs, () => null);
        ui.SetRenderer(new RecordingRenderer());
        return ui;
    }

    private static async Task<(VirtualFileSystem Vfs, string Path)> WriteUiAsync(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "cyberland_ui_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Content", "Ui"));
        var rel = "Content/Ui/doc.json";
        await File.WriteAllTextAsync(Path.Combine(dir, rel), json);
        var vfs = new VirtualFileSystem();
        vfs.Mount(dir);
        return (vfs, rel);
    }
}
