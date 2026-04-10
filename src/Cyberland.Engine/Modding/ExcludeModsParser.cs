namespace Cyberland.Engine.Modding;

/// <summary>
/// Parses <c>--exclude-mods modA,modB</c> from the host process command line so specific <see cref="ModManifest.Id"/> values are skipped during <see cref="ModLoader.LoadAll"/>.
/// </summary>
public static class ExcludeModsParser
{
    /// <summary>Long-form flag name (case-insensitive match).</summary>
    public const string Flag = "--exclude-mods";

    /// <summary>
    /// Reads argv-style arguments; returns excluded mod ids, or <c>null</c> if the flag was not used.
    /// </summary>
    /// <param name="args">Typically <c>Environment.GetCommandLineArgs()</c> without the program path, or equivalent.</param>
    /// <returns><c>null</c> if the flag is absent; an empty array if <c>--exclude-mods</c> is present with no list.</returns>
    public static string[]? TryParse(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals(Flag, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 >= args.Length)
                return Array.Empty<string>();

            var rest = args[i + 1].AsSpan();
            if (rest.IsWhiteSpace())
                return Array.Empty<string>();

            var parts = rest.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? Array.Empty<string>() : parts;
        }

        return null;
    }
}
