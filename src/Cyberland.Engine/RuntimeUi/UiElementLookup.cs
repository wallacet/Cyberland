using Cyberland.Engine.UI.Core;

namespace Cyberland.Engine.RuntimeUi;

/// <summary>Typed lookups into <see cref="UiBuildResult.ElementsById"/>.</summary>
public static class UiElementLookup
{
    /// <summary>Gets an element by <paramref name="id"/> or throws.</summary>
    public static T Require<T>(this IReadOnlyDictionary<string, UiElement> map, string id)
        where T : UiElement
    {
        if (!map.TryGetValue(id, out var el))
            throw new InvalidOperationException($"UI element id '{id}' was not found.");
        if (el is not T typed)
            throw new InvalidOperationException($"UI element id '{id}' is {el.GetType().Name}, not {typeof(T).Name}.");
        return typed;
    }

    /// <summary>Attempts to get a typed element by <paramref name="id"/>.</summary>
    public static bool TryGet<T>(this IReadOnlyDictionary<string, UiElement> map, string id, out T? element)
        where T : UiElement
    {
        element = null;
        if (!map.TryGetValue(id, out var el) || el is not T typed)
            return false;
        element = typed;
        return true;
    }
}
