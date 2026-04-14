using System.Globalization;

namespace Cyberland.Engine.Localization;

/// <summary>Resolves active primary culture: command line overrides <c>language.json</c>, default English.</summary>
public static class LanguagePreference
{
    /// <summary>
    /// <paramref name="commandLineArgs"/> should be argv without the program name (same as <see cref="GameApplication"/> ctor).
    /// </summary>
    public static string Resolve(ReadOnlySpan<string> commandLineArgs, string languageSettingsPath)
    {
        var fromCli = LanguageCommandLine.TryParseCulture(commandLineArgs);
        var raw = fromCli is not null
            ? LocalizationCultureChains.NormalizeCultureName(fromCli)
            : LanguageSettingsFile.LoadPrimaryCulture(languageSettingsPath);

        return TryValidateCultureName(raw);
    }

    /// <summary>Returns <paramref name="cultureName"/> if <see cref="CultureInfo"/> recognizes it; otherwise <see cref="LocalizationCultureChains.EnglishNeutral"/>.</summary>
    public static string TryValidateCultureName(string cultureName)
    {
        try
        {
            _ = CultureInfo.GetCultureInfo(cultureName);
            return cultureName;
        }
        catch (CultureNotFoundException)
        {
            return LocalizationCultureChains.EnglishNeutral;
        }
    }
}
