using System.Diagnostics.CodeAnalysis;
using Cyberland.Engine.Core.Ecs;
using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.Hosting;

/// <summary>
/// Maps ECS entities to retained <see cref="UiDocument"/> instances for <see cref="Scene.Systems.UiDocumentFrameSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Typical lifecycle: spawn one entity per screen/panel, attach <see cref="UI.Ecs.UiDocumentRoot"/>, then
/// <see cref="Register"/> the managed document instance. Unregister when closing the screen.
/// </para>
/// <para>
/// Registry mutations and reads are expected on the serial frame thread that owns UI document layout and draw
/// orchestration. The registry is intentionally lightweight and not synchronized for parallel worker access.
/// </para>
/// </remarks>
public sealed class UiDocumentRegistry
{
    private readonly Dictionary<EntityId, UiDocument> _documents = new();
    private readonly Dictionary<EntityId, IReadOnlyDictionary<string, UiElement>> _elementsById = new();
    private readonly HashSet<EntityId> _pruneKeepSet = new();

    /// <summary>Associates <paramref name="document"/> with <paramref name="entity"/> (replaces any prior entry).</summary>
    /// <param name="entity">Owning ECS entity.</param>
    /// <param name="document">Built UI document.</param>
    /// <param name="elementsById">Optional map from JSON <c>id</c> to nodes (from <see cref="RuntimeUi.UiBuildResult"/>).</param>
    public void Register(EntityId entity, UiDocument document, IReadOnlyDictionary<string, UiElement>? elementsById = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[entity] = document;
        if (elementsById is null)
            _elementsById.Remove(entity);
        else
            _elementsById[entity] = elementsById;
    }

    /// <summary>Resolves authored element ids for a registered document.</summary>
    public bool TryGetElements(EntityId entity, out IReadOnlyDictionary<string, UiElement> elementsById) =>
        _elementsById.TryGetValue(entity, out elementsById!);

    /// <summary>Removes a registration if present.</summary>
    public bool Unregister(EntityId entity)
    {
        _elementsById.Remove(entity);
        return _documents.Remove(entity);
    }

    /// <summary>Attempts to resolve a document for <paramref name="entity"/>.</summary>
    public bool TryGet(EntityId entity, [NotNullWhen(true)] out UiDocument? document) =>
        _documents.TryGetValue(entity, out document);

    /// <summary>
    /// Removes documents whose owning entity is no longer present in the active root set.
    /// </summary>
    public int PruneToEntities(ReadOnlySpan<EntityId> activeEntities)
    {
        _pruneKeepSet.Clear();
        for (var i = 0; i < activeEntities.Length; i++)
            _pruneKeepSet.Add(activeEntities[i]);

        if (_documents.Count == 0)
            return 0;

        List<EntityId>? remove = null;
        foreach (var key in _documents.Keys)
        {
            if (_pruneKeepSet.Contains(key))
                continue;
            remove ??= new List<EntityId>();
            remove.Add(key);
        }

        if (remove is null)
            return 0;

        foreach (var key in remove)
        {
            _documents.Remove(key);
            _elementsById.Remove(key);
        }

        return remove.Count;
    }
}
