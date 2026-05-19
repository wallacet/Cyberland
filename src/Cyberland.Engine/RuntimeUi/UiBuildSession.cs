using Cyberland.Engine.UI.Controls;
using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Per-build bookkeeping: element ids and shared radio groups.</summary>
public sealed class UiBuildSession
{
    private readonly Dictionary<string, UiElement> _elementsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiRadioGroup> _radioGroups = new(StringComparer.Ordinal);

    /// <summary>Resolved elements by authored <c>id</c>.</summary>
    public IReadOnlyDictionary<string, UiElement> ElementsById => _elementsById;

    /// <summary>Registers <paramref name="id"/> or throws when duplicate.</summary>
    public void RegisterElementId(string id, UiElement element)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(element);
        if (!_elementsById.TryAdd(id, element))
            throw new InvalidOperationException($"Duplicate UI element id '{id}'.");
    }

    /// <summary>Returns or creates a <see cref="UiRadioGroup"/> for <paramref name="groupId"/>.</summary>
    public UiRadioGroup GetOrCreateRadioGroup(string groupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (_radioGroups.TryGetValue(groupId, out var existing))
            return existing;
        var group = new UiRadioGroup();
        _radioGroups[groupId] = group;
        return group;
    }

    /// <summary>Gets a registered radio group.</summary>
    public bool TryGetRadioGroup(string groupId, out UiRadioGroup? group) =>
        _radioGroups.TryGetValue(groupId, out group);
}
