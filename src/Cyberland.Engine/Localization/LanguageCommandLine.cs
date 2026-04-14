namespace Cyberland.Engine.Localization;

/// <summary>Parses <c>--lang=de</c> or <c>--lang de</c> from host argv (restart applies).</summary>
public static class LanguageCommandLine
{
    /// <summary>Long-form flag (case-insensitive).</summary>
    public const string Flag = "--lang";

    /// <summary>Returns culture name if present, otherwise <c>null</c>.</summary>
    public static string? TryParseCulture(ReadOnlySpan<string> args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals(Flag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    return LocalizationCultureChains.EnglishNeutral;
                var next = args[i + 1].Trim();
                return string.IsNullOrEmpty(next) ? LocalizationCultureChains.EnglishNeutral : next;
            }

            if (a.StartsWith(Flag + "=", StringComparison.OrdinalIgnoreCase) && a.Length > Flag.Length + 1)
            {
                var v = a[(Flag.Length + 1)..].Trim();
                return string.IsNullOrEmpty(v) ? LocalizationCultureChains.EnglishNeutral : v;
            }
        }

        return null;
    }
}
