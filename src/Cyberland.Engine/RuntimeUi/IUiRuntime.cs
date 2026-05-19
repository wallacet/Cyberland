using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.UI.Core;
using System.Text.Json;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Host-facing runtime UI: VFS JSON documents, element deserializers, and entity attachment.</summary>
public interface IUiRuntime
{
    /// <summary>Parses a UI JSON file from the layered VFS and builds a <see cref="UI.Core.UiDocument"/> tree.</summary>
    ValueTask<UiBuildResult> LoadDocumentAsync(
        string uiPath,
        UiLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Loads <paramref name="uiPath"/> and registers the document on <paramref name="host"/>'s <see cref="UiDocumentRegistry"/>.</summary>
    ValueTask<UiAttachResult> AttachToEntityAsync(
        EntityId entity,
        string uiPath,
        GameHostServices host,
        UiLoadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Registers a mod element deserializer for UI JSON <c>type</c> strings.</summary>
    void RegisterElementDeserializer(string typeId, UiElementDeserializer deserializer);

    /// <summary>Registers a schema migrator from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.</summary>
    void RegisterSchemaMigrator(int fromVersion, int toVersion, UiSchemaMigrator migrator);
}

/// <summary>Builds one UI element from a JSON node.</summary>
public delegate UiElement UiElementDeserializer(in UiElementDeserializeContext context);

/// <summary>Context passed to <see cref="UiElementDeserializer"/>.</summary>
public readonly struct UiElementDeserializeContext
{
    /// <summary>Creates deserialize context for one JSON node.</summary>
    public UiElementDeserializeContext(
        JsonElement node,
        UI.Core.UiDocument document,
        UiBuildSession session,
        Rendering.IRenderer renderer,
        ILocalizedContentStrings? strings)
    {
        Node = node;
        Document = document;
        Session = session;
        Renderer = renderer;
        Strings = strings;
    }

    /// <summary>Current element JSON object.</summary>
    public JsonElement Node { get; }

    /// <summary>Target document (tree attaches under <see cref="UI.Core.UiDocument.Root"/>).</summary>
    public UI.Core.UiDocument Document { get; }

    /// <summary>Per-build id map and deferred wiring (radio groups).</summary>
    public UiBuildSession Session { get; }

    /// <summary>Renderer for builtin texture ids.</summary>
    public Rendering.IRenderer Renderer { get; }

    /// <summary>Optional localized strings (unused for literal text).</summary>
    public ILocalizedContentStrings? Strings { get; }
}

/// <summary>Migrates a UI document root from one schema version to the next.</summary>
public delegate JsonElement UiSchemaMigrator(JsonElement root);
