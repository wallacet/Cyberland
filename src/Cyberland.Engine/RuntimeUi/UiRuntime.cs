using System.Text.Json;
using Cyberland.Engine.Assets;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.Hosting;
using Cyberland.Engine.Localization;
using Cyberland.Engine.Rendering;
using Cyberland.Engine.RuntimeScenes;
using Cyberland.Engine.RuntimeUi.Serialization;
using Cyberland.Engine.Serialization;
using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Default <see cref="IUiRuntime"/>: VFS JSON load, element tree build, and registry attach.</summary>
public sealed class UiRuntime : IUiRuntime
{
    private readonly VirtualFileSystem _vfs;
    private readonly Func<ILocalizedContent?> _getLocalizedContent;
    private readonly Dictionary<string, UiElementDeserializer> _deserializers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int From, int To), UiSchemaMigrator> _migrators = new();
    private readonly object _gate = new();
    private IRenderer? _renderer;
    private bool _engineDeserializersRegistered;

    /// <summary>Constructs UI runtime bound to VFS and localization.</summary>
    public UiRuntime(VirtualFileSystem vfs, Func<ILocalizedContent?> getLocalizedContent)
    {
        _vfs = vfs;
        _getLocalizedContent = getLocalizedContent;
    }

    /// <summary>Supplies renderer for builtin textures and registers engine element types.</summary>
    public void SetRenderer(IRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
        EnsureEngineElementDeserializers(renderer);
    }

    /// <inheritdoc />
    public void RegisterElementDeserializer(string typeId, UiElementDeserializer deserializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(deserializer);
        lock (_gate)
            _deserializers[typeId] = deserializer;
    }

    /// <inheritdoc />
    public void RegisterSchemaMigrator(int fromVersion, int toVersion, UiSchemaMigrator migrator)
    {
        ArgumentNullException.ThrowIfNull(migrator);
        lock (_gate)
            _migrators[(fromVersion, toVersion)] = migrator;
    }

    /// <inheritdoc />
    public async ValueTask<UiBuildResult> LoadDocumentAsync(
        string uiPath,
        UiLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uiPath);
        options ??= new UiLoadOptions();
        var renderer = _renderer ?? throw new InvalidOperationException("UI runtime requires IRenderer before load.");
        EnsureEngineElementDeserializers(renderer);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assets = new AssetManager(_vfs);
            var text = await assets.LoadTextAsync(uiPath, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.Clone();
            var migrated = ApplyMigrators(root);
            var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<UiDocumentDto>(migrated.GetRawText(), jsonOpts)
                ?? throw new InvalidOperationException("UI document deserialized to null.");
            if (parsed.SchemaVersion > UiDocumentDto.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported newer schemaVersion={parsed.SchemaVersion}.");

            if (parsed.Root.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("UI document root must be a JSON object.");

            var strings = BuildStringsTable();
            var session = new UiBuildSession();
            var document = new UiDocument();
            IReadOnlyDictionary<string, UiElementDeserializer> map;
            lock (_gate)
                map = new Dictionary<string, UiElementDeserializer>(_deserializers);

            ApplyRootNode(document, parsed.Root, session, renderer, strings, map, options.AllowUnknownElementTypes);

            return new UiBuildResult
            {
                Succeeded = true,
                Document = document,
                Session = session
            };
        }
        catch (Exception ex)
        {
            return new UiBuildResult { Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async ValueTask<UiAttachResult> AttachToEntityAsync(
        EntityId entity,
        string uiPath,
        GameHostServices host,
        UiLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        var build = await LoadDocumentAsync(uiPath, options, cancellationToken).ConfigureAwait(false);
        if (!build.Succeeded || build.Document is null)
        {
            return new UiAttachResult
            {
                Succeeded = false,
                Build = build,
                ErrorMessage = build.ErrorMessage ?? "UI build failed."
            };
        }

        host.UiDocuments.Register(entity, build.Document, build.ElementsById);
        return new UiAttachResult { Succeeded = true, Build = build };
    }

    internal static UiElement BuildElement(
        JsonElement node,
        UiDocument document,
        UiBuildSession session,
        IRenderer renderer,
        ILocalizedContentStrings? strings,
        IReadOnlyDictionary<string, UiElementDeserializer> deserializers,
        bool allowUnknownElementTypes)
    {
        var type = RuntimeJsonReaders.ReadString(node, "type");
        if (string.IsNullOrWhiteSpace(type))
            throw new InvalidOperationException("UI element requires type.");

        if (!deserializers.TryGetValue(type, out var del))
        {
            if (allowUnknownElementTypes)
                return new UiPanel();
            throw new InvalidOperationException($"Unknown UI element type '{type}'.");
        }

        var ctx = new UiElementDeserializeContext(node, document, session, renderer, strings);
        var element = del(in ctx);
        EngineUiElementDeserializers.ApplySharedElementFields(element, node, session);
        EngineUiElementDeserializers.ApplyTypedFields(element, node, renderer);

        if (element is UiScrollView scroll)
        {
            EngineUiElementDeserializers.BuildScrollViewContent(
                scroll, node, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
        }
        else
        {
            EngineUiElementDeserializers.BuildChildren(
                element, node, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
        }

        return element;
    }

    internal static void BuildAndAttachChild(
        UiElement parent,
        JsonElement childNode,
        UiDocument document,
        UiBuildSession session,
        IRenderer renderer,
        ILocalizedContentStrings? strings,
        IReadOnlyDictionary<string, UiElementDeserializer> deserializers,
        bool allowUnknownElementTypes)
    {
        var child = BuildElement(childNode, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
        parent.AddChild(child);
    }

    private void ApplyRootNode(
        UiDocument document,
        JsonElement rootNode,
        UiBuildSession session,
        IRenderer renderer,
        ILocalizedContentStrings? strings,
        IReadOnlyDictionary<string, UiElementDeserializer> deserializers,
        bool allowUnknownElementTypes)
    {
        var root = document.Root;
        EngineUiElementDeserializers.ApplySharedElementFields(root, rootNode, session);
        EngineUiElementDeserializers.ApplyTypedFields(root, rootNode, renderer);
        EngineUiElementDeserializers.BuildChildren(
            root, rootNode, document, session, renderer, strings, deserializers, allowUnknownElementTypes);
    }

    private void EnsureEngineElementDeserializers(IRenderer renderer)
    {
        if (_engineDeserializersRegistered)
            return;
        EngineUiElementDeserializers.Register(this, renderer);
        _engineDeserializersRegistered = true;
    }

    private ILocalizedContentStrings? BuildStringsTable()
    {
        var loc = _getLocalizedContent();
        return loc is null ? null : new LocalizationManagerStringTable(loc.Strings);
    }

    private JsonElement ApplyMigrators(JsonElement root)
    {
        var version = root.TryGetProperty("schemaVersion", out var sv) && sv.TryGetInt32(out var v) ? v : 0;
        var el = root;
        while (version < UiDocumentDto.CurrentSchemaVersion)
        {
            UiSchemaMigrator? mig;
            lock (_gate)
                _migrators.TryGetValue((version, version + 1), out mig);
            if (mig is null)
                throw new InvalidOperationException($"Missing UI migrator {version}->{version + 1}.");
            el = mig(el);
            version++;
        }

        return el;
    }
}
