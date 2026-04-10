namespace Cyberland.Engine.Modding;

/// <summary>Parses <c>--exclude-mods id1,id2</c> from host command-line args.</summary>
public static class ExcludeModsParser
{
    public const string Flag = "--exclude-mods";

    /// <summary>Returns null if the flag is absent; empty array if present with no ids.</summary>
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
