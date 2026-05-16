using System.Text.Json;
using Cyberland.Engine.Localization;

namespace Cyberland.Engine.RuntimeScenes;

/// <summary>
/// Bridges <see cref="LocalizationManager"/> to <see cref="ILocalizedContentStrings"/> for scene deserialization.
/// </summary>
public sealed class LocalizationManagerStringTable : ILocalizedContentStrings
{
    private readonly LocalizationManager _strings;

    /// <summary>Wraps merged string keys.</summary>
    public LocalizationManagerStringTable(LocalizationManager strings) => _strings = strings;

    /// <inheritdoc />
    public bool TryGetString(string key, out string value) => _strings.TryGet(key, out value!);
}
