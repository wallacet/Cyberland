using System.Globalization;

namespace Cyberland.Engine.Localization;

/// <summary>
/// Builds culture lists for merging JSON string tables (general → specific, last wins) and for resolving
/// localized binary assets (specific → general → unlocalized, first existing wins).
/// </summary>
public static class LocalizationCultureChains
{
    /// <summary>Fixed ultimate fallback for string keys and non-localized assets when no culture-specific file exists.</summary>
    public const string EnglishNeutral = "en";

    /// <summary>
    /// Cultures to merge JSON files in order: <c>Locale/en/file.json</c> first, then parent cultures up to <paramref name="primaryCultureName"/>, last merge wins per key.
    /// </summary>
    public static IReadOnlyList<string> StringTableMergeOrder(string primaryCultureName)
    {
        var primary = NormalizeCultureName(primaryCultureName);
        if (string.Equals(primary, EnglishNeutral, StringComparison.OrdinalIgnoreCase))
            return new[] { EnglishNeutral };

        var tail = new List<string>();
        var c = CultureInfo.GetCultureInfo(primary);

        while (!string.IsNullOrEmpty(c.Name) && !c.Equals(CultureInfo.InvariantCulture))
        {
            if (!c.Name.Equals(EnglishNeutral, StringComparison.OrdinalIgnoreCase))
                tail.Add(c.Name);
            c = c.Parent;
        }

        tail.Reverse();
        var order = new List<string> { EnglishNeutral };
        order.AddRange(tail);
        return order;
    }

    /// <summary>
    /// Order to probe for an existing localized asset: most specific first, then <c>en</c>, then unqualified path.
    /// </summary>
    public static IReadOnlyList<string> AssetResolutionCultureOrder(string primaryCultureName)
    {
        var merge = StringTableMergeOrder(primaryCultureName);
        var probe = new List<string>();
        for (var i = merge.Count - 1; i >= 0; i--)
            probe.Add(merge[i]);
        return probe;
    }

    /// <summary>Normalizes empty/invalid to <see cref="EnglishNeutral"/>.</summary>
    public static string NormalizeCultureName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return EnglishNeutral;
        return cultureName.Trim();
    }
}
