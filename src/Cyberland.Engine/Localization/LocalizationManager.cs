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

    public CultureInfo Culture => _culture;

    public void SetCulture(CultureInfo culture) => _culture = culture;

    /// <summary>
    /// Merge a JSON object of key → string into the active dictionary (later loads override earlier keys).
    /// </summary>
    public void MergeJson(ReadOnlyMemory<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json);
        MergeJson(doc.RootElement);
    }

    public void MergeJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in root.EnumerateObject())
            _merged[prop.Name] = prop.Value.GetString() ?? string.Empty;
    }

    public string Get(string key) =>
        _merged.TryGetValue(key, out var value) ? value : key;

    public bool TryGet(string key, out string value) => _merged.TryGetValue(key, out value!);

    /// <summary>Removes a key if present. Returns whether it existed.</summary>
    public bool TryRemoveKey(string key) => _merged.Remove(key);

    /// <summary>Removes a key if present.</summary>
    public void RemoveKey(string key) => _merged.Remove(key);

    public void Clear() => _merged.Clear();
}
