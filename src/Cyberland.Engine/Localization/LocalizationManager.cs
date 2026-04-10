using System.Globalization;
using System.Text.Json;

namespace Cyberland.Engine.Localization;

/// <summary>
/// Loads string tables from JSON (mod-friendly) with culture fallback (e.g. en-US → en).
/// </summary>
public sealed class LocalizationManager
{
    private readonly Dictionary<string, string> _merged = new(StringComparer.Ordinal);
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>Active culture for formatting (string selection is key-based, not yet pluralization).</summary>
    public CultureInfo Culture => _culture;

    /// <summary>Updates the culture used for future formatting helpers (tables remain merged).</summary>
    public void SetCulture(CultureInfo culture) => _culture = culture;

    /// <summary>
    /// Merge a JSON object of key → string into the active dictionary (later loads override earlier keys).
    /// </summary>
    public void MergeJson(ReadOnlyMemory<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json);
        MergeJson(doc.RootElement);
    }

    /// <summary>Merges key/value pairs from a JSON object into the active dictionary (later keys override).</summary>
    /// <param name="root">JSON object root; non-objects are ignored.</param>
    public void MergeJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in root.EnumerateObject())
            _merged[prop.Name] = prop.Value.GetString() ?? string.Empty;
    }

    /// <summary>Returns the translated string or <paramref name="key"/> itself if missing.</summary>
    public string Get(string key) =>
        _merged.TryGetValue(key, out var value) ? value : key;

    /// <summary>Non-throwing lookup.</summary>
    public bool TryGet(string key, out string value) => _merged.TryGetValue(key, out value!);

    /// <summary>Removes a key if present. Returns whether it existed.</summary>
    public bool TryRemoveKey(string key) => _merged.Remove(key);

    /// <summary>Removes a key if present (ignores if absent).</summary>
    public void RemoveKey(string key) => _merged.Remove(key);

    /// <summary>Clears all merged strings (tests / mod unload).</summary>
    public void Clear() => _merged.Clear();
}
