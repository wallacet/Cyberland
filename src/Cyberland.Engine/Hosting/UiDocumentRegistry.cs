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
/// </remarks>
public sealed class UiDocumentRegistry
{
    private readonly Dictionary<EntityId, UiDocument> _documents = new();

    /// <summary>Associates <paramref name="document"/> with <paramref name="entity"/> (replaces any prior entry).</summary>
    public void Register(EntityId entity, UiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents[entity] = document;
    }

    /// <summary>Removes a registration if present.</summary>
    public bool Unregister(EntityId entity) => _documents.Remove(entity);

    /// <summary>Attempts to resolve a document for <paramref name="entity"/>.</summary>
    public bool TryGet(EntityId entity, [NotNullWhen(true)] out UiDocument? document) =>
        _documents.TryGetValue(entity, out document);
}
