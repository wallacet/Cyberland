using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyberland.Engine.Localization;

/// <summary>Persists <c>primaryCulture</c> in <c>language.json</c> beside the executable (restart to apply).</summary>
public static class LanguageSettingsFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Reads culture from disk, or returns <see cref="LocalizationCultureChains.EnglishNeutral"/> if missing/invalid.</summary>
    public static string LoadPrimaryCulture(string absolutePath)
    {
        try
        {
            if (!File.Exists(absolutePath))
                return LocalizationCultureChains.EnglishNeutral;

            var json = File.ReadAllText(absolutePath);
            var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions);
            if (dto?.PrimaryCulture is { Length: > 0 } c)
                return LocalizationCultureChains.NormalizeCultureName(c);
        }
        catch
        {
            // Corrupt or locked file — fall back to English.
        }

        return LocalizationCultureChains.EnglishNeutral;
    }

    /// <summary>Writes the current preference (optional; for future settings UI).</summary>
    public static void SavePrimaryCulture(string absolutePath, string primaryCulture)
    {
        var dir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var dto = new Dto { PrimaryCulture = LocalizationCultureChains.NormalizeCultureName(primaryCulture) };
        File.WriteAllText(absolutePath, JsonSerializer.Serialize(dto, JsonOptions));
    }

    private sealed class Dto
    {
        public string? PrimaryCulture { get; set; }
    }
}
